import { WebSocketServer, WebSocket } from "ws";
import { randomUUID } from "crypto";
import {
  JsonRpcRequest,
  JsonRpcResponse,
  PendingRequest,
} from "./utils/types.js";
import {
  GodotConnectionError,
  GodotCommandError,
  TimeoutError,
} from "./utils/errors.js";

const BASE_PORT = 6505;
const MAX_PORT = 6509;
const COMMAND_TIMEOUT_MS = 30000;
const HEARTBEAT_INTERVAL_MS = 10000;
const HEARTBEAT_TIMEOUT_MS = HEARTBEAT_INTERVAL_MS * 3;
const TCP_KEEPALIVE_DELAY_MS = 5000;

export class GodotConnection {
  private wss: WebSocketServer | null = null;
  private client: WebSocket | null = null;
  private port: number;
  private fixedPort: boolean;
  private basePort: number;
  private maxPort: number;
  private pendingRequests: Map<string, PendingRequest> = new Map();
  private heartbeatTimer: ReturnType<typeof setInterval> | null = null;
  private lastPongAt: number = 0;

  constructor(
    port: number = BASE_PORT,
    fixedPort: boolean = false,
    options: { basePort?: number; maxPort?: number } = {}
  ) {
    this.port = port;
    this.fixedPort = fixedPort;
    this.basePort = options.basePort ?? BASE_PORT;
    this.maxPort = options.maxPort ?? MAX_PORT;
  }

  /** Start WebSocket server, retrying on the next port if the first bind races. */
  async connect(): Promise<void> {
    if (this.wss) return;

    const candidates = this.fixedPort
      ? [this.port]
      : Array.from({ length: this.maxPort - this.basePort + 1 }, (_, i) => this.basePort + i);

    let lastError: Error | null = null;
    for (const port of candidates) {
      try {
        const wss = await this.bindWebSocketServer(port);
        this.wss = wss;
        this.port = port;
        this.attachConnectionHandler(wss);
        console.error(
          `[MCP] WebSocket server listening on ws://127.0.0.1:${port}`
        );
        return;
      } catch (err) {
        lastError = err as Error;
        // EADDRINUSE means another MCP server (likely a parallel Claude session)
        // won the bind race. Silently try the next port. Other errors are
        // logged so we don't swallow real config problems.
        if ((err as NodeJS.ErrnoException).code !== "EADDRINUSE") {
          console.error(
            `[MCP] Bind failed on port ${port}: ${(err as Error).message}`
          );
        }
      }
    }

    const range = this.fixedPort
      ? String(this.port)
      : `${this.basePort}-${this.maxPort}`;
    const hint = this.fixedPort
      ? "Try removing GODOT_MCP_PORT from your client config to enable auto-scanning, or kill the process holding the port."
      : "All ports are occupied — likely too many parallel Claude Code sessions or stale node MCP processes.";
    throw new GodotConnectionError(
      `Failed to bind WebSocket server on port range ${range}. ` +
      `Last error: ${lastError?.message ?? "unknown"}. ${hint}`
    );
  }

  /** Try to bind a single WebSocketServer. Resolves once 'listening' fires, rejects on bind error. */
  private bindWebSocketServer(port: number): Promise<WebSocketServer> {
    return new Promise<WebSocketServer>((resolve, reject) => {
      const wss = new WebSocketServer({ port, host: "127.0.0.1" });

      const onError = (err: Error) => {
        wss.off("listening", onListening);
        wss.close();
        reject(err);
      };
      const onListening = () => {
        wss.off("error", onError);
        // Re-attach a runtime error handler now that the server is live.
        // Pre-bind errors fail the connect attempt; post-bind errors are logged.
        wss.on("error", (err: Error) => {
          console.error("[MCP] WebSocket server error:", err.message);
        });
        resolve(wss);
      };

      wss.once("error", onError);
      wss.once("listening", onListening);
    });
  }

  private attachConnectionHandler(wss: WebSocketServer): void {
    wss.on("connection", (ws: WebSocket) => {
      console.error("[MCP] Godot editor connected");

      // Enable OS-level TCP keepalive so half-open sockets surface faster
      // than the Windows default (~2 hours). Application-level heartbeat
      // below is still the primary detection mechanism.
      const sock = (ws as unknown as { _socket?: { setKeepAlive?: (enable: boolean, initialDelay: number) => void } })._socket;
      sock?.setKeepAlive?.(true, TCP_KEEPALIVE_DELAY_MS);

      if (this.client) {
        this.client.close(1000, "Replaced by new connection");
      }
      this.client = ws;
      this.lastPongAt = Date.now();
      this.startHeartbeat();

      ws.on("message", (data: Buffer) => {
        this.handleMessage(data.toString());
      });

      ws.on("close", () => {
        console.error("[MCP] Godot editor disconnected");
        if (this.client === ws) {
          this.client = null;
          this.stopHeartbeat();
          this.rejectAllPending(
            new GodotConnectionError("Godot disconnected")
          );
        }
      });

      ws.on("error", (err: Error) => {
        console.error("[MCP] WebSocket error:", err.message);
      });
    });
  }

  disconnect(): void {
    this.stopHeartbeat();
    if (this.client) {
      this.client.close(1000, "Server shutting down");
      this.client = null;
    }
    if (this.wss) {
      this.wss.close();
      this.wss = null;
    }
    this.rejectAllPending(new GodotConnectionError("Server shut down"));
  }

  isConnected(): boolean {
    return this.client?.readyState === WebSocket.OPEN;
  }

  getPort(): number {
    return this.port;
  }

  async sendCommand(
    method: string,
    params: Record<string, unknown> = {}
  ): Promise<unknown> {
    if (!this.isConnected()) {
      throw new GodotConnectionError(
        "Godot editor is not connected. Make sure the Godot MCP Pro plugin is enabled and the editor is running."
      );
    }

    const id = randomUUID();
    const request: JsonRpcRequest = {
      jsonrpc: "2.0",
      method,
      params,
      id,
    };

    return new Promise<unknown>((resolve, reject) => {
      const timer = setTimeout(() => {
        this.pendingRequests.delete(id);
        reject(new TimeoutError(method, COMMAND_TIMEOUT_MS));
      }, COMMAND_TIMEOUT_MS);

      this.pendingRequests.set(id, {
        resolve: resolve as (value: JsonRpcResponse) => void,
        reject,
        timer,
      });
      this.client!.send(JSON.stringify(request));
    });
  }

  private handleMessage(data: string): void {
    let msg: JsonRpcResponse;
    try {
      msg = JSON.parse(data);
    } catch {
      console.error("[MCP] Failed to parse message from Godot:", data);
      return;
    }

    const method = (msg as unknown as { method?: string }).method;
    if (method === "pong") {
      this.lastPongAt = Date.now();
      return;
    }

    // Godot may also send unsolicited pings — reply so its inactivity timer resets
    if (method === "ping") {
      this.lastPongAt = Date.now();
      if (this.isConnected()) {
        this.client!.send(JSON.stringify({ jsonrpc: "2.0", method: "pong", params: {} }));
      }
      return;
    }

    if (!msg.id) return;

    const pending = this.pendingRequests.get(msg.id);
    if (!pending) return;

    clearTimeout(pending.timer);
    this.pendingRequests.delete(msg.id);

    if (msg.error) {
      pending.reject(
        new GodotCommandError(
          msg.error.code,
          msg.error.message,
          msg.error.data
        )
      );
    } else {
      pending.resolve(msg.result as unknown as JsonRpcResponse);
    }
  }

  private rejectAllPending(error: Error): void {
    for (const [, pending] of this.pendingRequests) {
      clearTimeout(pending.timer);
      pending.reject(error);
    }
    this.pendingRequests.clear();
  }

  private startHeartbeat(): void {
    this.stopHeartbeat();
    this.heartbeatTimer = setInterval(() => {
      if (!this.isConnected()) return;

      // If Godot has been silent for too long, the socket is likely half-open.
      // terminate() forcibly destroys the TCP socket (vs close() which waits
      // for a FIN ack that will never arrive on a dead link).
      if (Date.now() - this.lastPongAt > HEARTBEAT_TIMEOUT_MS) {
        console.error(
          `[MCP] Heartbeat timeout (no pong for ${HEARTBEAT_TIMEOUT_MS}ms) — terminating dead connection`
        );
        const dead = this.client;
        this.client = null;
        this.stopHeartbeat();
        this.rejectAllPending(
          new GodotConnectionError("Heartbeat timeout — Godot connection lost")
        );
        dead?.terminate();
        return;
      }

      this.client!.send(
        JSON.stringify({ jsonrpc: "2.0", method: "ping", params: {} })
      );
    }, HEARTBEAT_INTERVAL_MS);
  }

  private stopHeartbeat(): void {
    if (this.heartbeatTimer) {
      clearInterval(this.heartbeatTimer);
      this.heartbeatTimer = null;
    }
  }
}

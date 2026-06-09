# Visual Reconnect Manual Checks

这个入口用于人工观察方案里的这条验收项：

- 重连发生在 Host 正播放出牌动画、技能动画或结算动画时：
  - 客户端先显示同步遮罩
  - 画面不出现明显闪跳
  - 遮罩移除后局面与 Host 一致

## 运行方式

```bash
bash tests/run_lan_reconnect_during_visual_manual.sh
```

可选：

```bash
CODEX_VISUAL_MANUAL_HOLD_SECONDS=8 bash tests/run_lan_reconnect_during_visual_manual.sh
```

如果需要让 Host 明显晚于 Client 退出，避免观察窗口尾部再次触发重连遮罩：

```bash
CODEX_VISUAL_MANUAL_HOLD_SECONDS=8 CODEX_VISUAL_HOST_HOLD_SECONDS=14 bash tests/run_lan_reconnect_during_visual_manual.sh
```

如果需要把遮罩帧和恢复后帧落盘用于复核：

```bash
CODEX_VISUAL_CAPTURE_DIR=/private/tmp/pokegame_visual_capture CODEX_VISUAL_MANUAL_HOLD_SECONDS=8 CODEX_VISUAL_HOST_HOLD_SECONDS=14 bash tests/run_lan_reconnect_during_visual_manual.sh
```

默认会同时启动两个可见窗口：

- Host：`LanReconnectDuringVisualHost.gd`
- Client：`LanReconnectDuringVisualClient.gd`

并在客户端重连成功后额外保留几秒，方便人工观察遮罩移除后的画面稳定性。

当设置了 `CODEX_VISUAL_CAPTURE_DIR` 时，客户端会额外保存：

- `client_overlay.png`
- `client_recovered.png`
- `client_recovered_stable.png`

其中：

- `client_recovered.png`
  - 更接近遮罩刚退出后的恢复时刻，可能仍落在过渡边界帧上
- `client_recovered_stable.png`
  - 额外延后一次后保存，优先用于人工确认“遮罩已经退出、棋盘进入稳定态”

这些 PNG 只作为辅助复核材料。由于窗口渲染与截图读回时机仍可能存在轻微非确定性，最终是否存在“明显闪跳”仍以实时人工观察为准。

## 人工检查点

客户端窗口应满足：

1. 断线进入重连后先看到同步遮罩。
2. 遮罩期间不会裸露出半同步的底层牌面跳变。
3. 遮罩移除后，桌面、手牌、回合归属与 Host 一致。
4. 不应看到明显的“先跳到最终态，再回退一帧，再跳回来”的闪跳。

## 已有自动语义验证

自动 smoke `tests/run_lan_reconnect_during_visual_smoke.sh` 已验证：

- `CLIENT_SAW_OVERLAY`
- `CLIENT_SAW_CATCHUP_PENDING`
- `CLIENT_SAW_VISUAL_QUEUE`
- `CLIENT_RECONNECTED`

但这些只能证明内部状态链路正确，不能替代人工视觉质量判断。

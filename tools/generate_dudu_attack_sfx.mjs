import fs from "fs";
import path from "path";
import https from "https";
import { fileURLToPath } from "url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const repoRoot = path.resolve(__dirname, "..");

const apiKey = process.env.ELEVENLABS_API_KEY;
if (!apiKey) {
  console.error("Missing ELEVENLABS_API_KEY");
  process.exit(1);
}

const promptPath = process.argv[2]
  ? path.resolve(repoRoot, process.argv[2])
  : path.join(repoRoot, "tools", "prompts", "dudu_attack_sfx_prompt.txt");
const outputPath = process.argv[3]
  ? path.resolve(repoRoot, process.argv[3])
  : path.join(repoRoot, "asset", "audio", "sfx", "adventure", "crab_pincer.mp3");
const tempPath = path.join(repoRoot, "tmp", path.basename(outputPath, path.extname(outputPath)) + ".generated.mp3");

const prompt = fs.readFileSync(promptPath, "utf8").trim();
fs.mkdirSync(path.dirname(tempPath), { recursive: true });

const body = JSON.stringify({
  text: prompt,
  model_id: "eleven_text_to_sound_v2",
  duration_seconds: 1.2,
  prompt_influence: 0.75,
});

const req = https.request(
  {
    hostname: "api.elevenlabs.io",
    path: "/v1/sound-generation",
    method: "POST",
    headers: {
      "xi-api-key": apiKey,
      "Content-Type": "application/json",
      "Content-Length": Buffer.byteLength(body),
    },
  },
  (res) => {
    if (res.statusCode !== 200) {
      let errorBody = "";
      res.setEncoding("utf8");
      res.on("data", (chunk) => {
        errorBody += chunk;
      });
      res.on("end", () => {
        console.error(`ElevenLabs request failed: ${res.statusCode}`);
        if (errorBody) {
          console.error(errorBody);
        }
        process.exit(1);
      });
      return;
    }

    const out = fs.createWriteStream(tempPath);
    res.pipe(out);
    out.on("finish", () => {
      out.close();
      fs.copyFileSync(tempPath, outputPath);
      console.log(`Wrote ${path.relative(repoRoot, outputPath)}`);
    });
  }
);

req.on("error", (error) => {
  console.error(`Network error: ${error.message}`);
  process.exit(1);
});

req.write(body);
req.end();

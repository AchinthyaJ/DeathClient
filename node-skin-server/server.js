const http = require("http");
const fs = require("fs");
const path = require("path");

function readArg(flag, fallback) {
  const index = process.argv.indexOf(flag);
  if (index === -1 || index + 1 >= process.argv.length) {
    return fallback;
  }

  return process.argv[index + 1];
}

const port = Number.parseInt(readArg("--port", "47135"), 10);
const storageDir = readArg(
  "--storage",
  path.join(process.env.HOME || process.cwd(), ".death-client", "skin-server"),
);

fs.mkdirSync(storageDir, { recursive: true });

const skinPath = path.join(storageDir, "current-skin.png");
const capePath = path.join(storageDir, "current-cape.png");

function sendJson(res, statusCode, payload) {
  const body = Buffer.from(JSON.stringify(payload, null, 2));
  res.writeHead(statusCode, {
    "Content-Type": "application/json; charset=utf-8",
    "Cache-Control": "no-store",
    "Content-Length": body.length,
  });
  res.end(body);
}

function sendFile(res, filePath) {
  if (!fs.existsSync(filePath)) {
    sendJson(res, 404, { error: "Not found" });
    return;
  }

  const stat = fs.statSync(filePath);
  res.writeHead(200, {
    "Content-Type": "image/png",
    "Cache-Control": "no-store",
    "Content-Length": stat.size,
  });

  fs.createReadStream(filePath).pipe(res);
}

const server = http.createServer((req, res) => {
  const url = new URL(req.url, `http://${req.headers.host || "127.0.0.1"}`);

  if (req.method !== "GET") {
    sendJson(res, 405, { error: "Method not allowed" });
    return;
  }

  if (url.pathname === "/health") {
    sendJson(res, 200, {
      ok: true,
      storageDir,
      skinPresent: fs.existsSync(skinPath),
      capePresent: fs.existsSync(capePath),
    });
    return;
  }

  if (url.pathname === "/v1/skins/current") {
    sendFile(res, skinPath);
    return;
  }

  if (url.pathname === "/v1/capes/current") {
    sendFile(res, capePath);
    return;
  }

  sendJson(res, 404, { error: "Unknown endpoint" });
});

server.listen(port, "127.0.0.1", () => {
  console.log(`listening on http://127.0.0.1:${port}`);
});

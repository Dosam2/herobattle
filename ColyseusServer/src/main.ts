import { Server } from "colyseus";
import { WebSocketTransport } from "@colyseus/ws-transport";
import { monitor } from "@colyseus/monitor";
import { createServer } from "http";
import express from "express";
import { GameRoom } from "./rooms/GameRoom.js";

// ─── 포트 설정 ───
const GAME_PORT = parseInt(process.env.GAME_PORT) || 7777;
const MONITOR_PORT = parseInt(process.env.MONITOR_PORT) || 8888;

// ═══════════════════════════════════════
// 1) 게임 서버 (WebSocket) - 포트 7777
// ═══════════════════════════════════════
const gameApp = express();

// CORS 허용 (Unity 클라이언트 연결용)
gameApp.use((req, res, next) => {
    res.header("Access-Control-Allow-Origin", "*");
    res.header("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
    res.header("Access-Control-Allow-Headers", "Content-Type");
    if (req.method === "OPTIONS") { res.sendStatus(200); return; }
    next();
});

// 헬스 체크 엔드포인트
gameApp.get("/health", (req, res) => {
    res.json({ status: "ok", uptime: process.uptime(), port: GAME_PORT, version: "0.17" });
});

const gameHttpServer = createServer(gameApp);

const gameServer = new Server({
    transport: new WebSocketTransport({
        server: gameHttpServer,
        pingInterval: 5000,
        pingMaxRetries: 5,
    }),
});

// 1:1 PVP 게임 룸 등록
gameServer.define("game_room", GameRoom)
    .enableRealtimeListing();

gameServer.listen(GAME_PORT);

// ═══════════════════════════════════════
// 2) 모니터 대시보드 - 포트 8888
// ═══════════════════════════════════════
const monitorApp = express();
monitorApp.use("/", monitor());
monitorApp.listen(MONITOR_PORT, () => {
    console.log(`━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━`);
    console.log(`  HeroBattle Colyseus Server v0.17`);
    console.log(`  Game Server : port ${GAME_PORT}`);
    console.log(`  Monitor     : port ${MONITOR_PORT}`);
    console.log(`━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━`);
});

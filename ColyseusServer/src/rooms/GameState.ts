import { Schema, type, MapSchema } from "@colyseus/schema";

/**
 * 개별 플레이어 상태
 * Colyseus 스키마로 자동 동기화됨
 */
export class PlayerState extends Schema {
    @type("number") playerNumber: number = 0;
    @type("number") x: number = 0;
    @type("number") y: number = 0.5;
    @type("number") z: number = 0;
    @type("number") hp: number = 100;
    @type("number") baseHp: number = 500;
    @type("boolean") isReady: boolean = false;
    @type("boolean") isAlive: boolean = true;
}

/**
 * 게임 룸 전체 상태
 * phase: "waiting" → "playing" → "ended"
 */
export class GameState extends Schema {
    @type({ map: PlayerState }) players = new MapSchema<PlayerState>();
    @type("string") phase: string = "waiting";
    @type("number") winner: number = 0;
    @type("number") elapsedTime: number = 0;
}

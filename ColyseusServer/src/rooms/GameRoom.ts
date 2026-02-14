import { Room, Client } from "colyseus";
import { GameState, PlayerState } from "./GameState.js";

/**
 * HeroBattle 1:1 PVP 게임 룸 (Colyseus 0.17)
 * - 최대 2명 매칭
 * - 2명 입장 시 자동 게임 시작
 * - 메시지 릴레이 + 상태 동기화
 */
export class GameRoom extends Room {
    maxClients = 2;

    // 좌석 예약 타임아웃 (모바일 네트워크 고려)
    seatReservationTimeout = 30;

    // 0.17: state를 프로퍼티로 직접 할당
    state = new GameState();

    private nextPlayerNumber = 1;

    onCreate(options: any) {
        console.log(`[GameRoom] 룸 생성: ${this.roomId}`);

        // ─── 게임 메시지 핸들러 (릴레이) ───

        // 플레이어 이동
        this.onMessage("move", (client, data) => {
            const pNum = this.getPlayerNumber(client);
            this.broadcast("move", { from: pNum, ...data }, { except: client });
        });

        // 유닛 소환
        this.onMessage("spawn", (client, data) => {
            const pNum = this.getPlayerNumber(client);
            this.broadcast("spawn", { from: pNum, ...data }, { except: client });
        });

        // 공격
        this.onMessage("attack", (client, data) => {
            const pNum = this.getPlayerNumber(client);
            this.broadcast("attack", { from: pNum, ...data }, { except: client });
        });

        // 기지 HP 동기화
        this.onMessage("base_hp", (client, data) => {
            const pNum = this.getPlayerNumber(client);
            const player = this.state.players.get(client.sessionId);
            if (player && data.hp !== undefined) {
                player.baseHp = data.hp;
            }
            this.broadcast("base_hp", { from: pNum, ...data }, { except: client });
        });

        // 유닛 HP 동기화
        this.onMessage("unit_hp", (client, data) => {
            const pNum = this.getPlayerNumber(client);
            this.broadcast("unit_hp", { from: pNum, ...data }, { except: client });
        });

        // 플레이어 HP 동기화
        this.onMessage("player_hp", (client, data) => {
            const pNum = this.getPlayerNumber(client);
            const player = this.state.players.get(client.sessionId);
            if (player && data.hp !== undefined) {
                player.hp = data.hp;
            }
            this.broadcast("player_hp", { from: pNum, ...data }, { except: client });
        });

        // 플레이어 위치 동기화
        this.onMessage("player_position", (client, data) => {
            const pNum = this.getPlayerNumber(client);
            const player = this.state.players.get(client.sessionId);
            if (player) {
                if (data.x !== undefined) player.x = data.x;
                if (data.y !== undefined) player.y = data.y;
                if (data.z !== undefined) player.z = data.z;
            }
            this.broadcast("player_position", { from: pNum, ...data }, { except: client });
        });

        // 기지 파괴 알림
        this.onMessage("base_destroyed", (client, data) => {
            const pNum = this.getPlayerNumber(client);
            console.log(`[GameRoom] Player ${pNum} 기지 파괴 보고`);

            this.state.winner = pNum;
            this.state.phase = "ended";

            this.broadcast("base_destroyed", { from: pNum, winner: pNum, ...data });
        });

        // 범용 동기화 메시지
        this.onMessage("sync", (client, data) => {
            const pNum = this.getPlayerNumber(client);
            this.broadcast("sync", { from: pNum, ...data }, { except: client });
        });

        // 게임 시간 업데이트
        this.setSimulationInterval((deltaTime) => {
            if (this.state.phase === "playing") {
                this.state.elapsedTime += deltaTime / 1000;
            }
        }, 1000);
    }

    onJoin(client: Client, options: any) {
        const playerNumber = this.nextPlayerNumber++;

        const player = new PlayerState();
        player.playerNumber = playerNumber;
        player.isReady = true;
        this.state.players.set(client.sessionId, player);

        client.send("joined", { playerNumber, roomId: this.roomId });
        console.log(`[GameRoom] Player ${playerNumber} (${client.sessionId}) 입장`);

        if (this.state.players.size === this.maxClients) {
            this.state.phase = "playing";
            this.broadcast("game_start", { playerCount: this.state.players.size });
            this.lock();
            console.log(`[GameRoom] 게임 시작! (2명 매칭 완료)`);
        }
    }

    // 0.17: onLeave의 두번째 파라미터가 consented: boolean → code: number 로 변경
    onLeave(client: Client, code: number) {
        const player = this.state.players.get(client.sessionId);
        if (player) {
            console.log(`[GameRoom] Player ${player.playerNumber} 퇴장 (code: ${code})`);
            player.isAlive = false;

            this.broadcast("player_left", { playerNumber: player.playerNumber });
            this.state.players.delete(client.sessionId);
        }

        if (this.state.phase === "playing" && this.state.players.size === 1) {
            this.state.players.forEach((remainingPlayer) => {
                this.state.winner = remainingPlayer.playerNumber;
            });
            this.state.phase = "ended";
            this.broadcast("game_ended", {
                reason: "opponent_left",
                winner: this.state.winner
            });
            console.log(`[GameRoom] 상대 퇴장으로 Player ${this.state.winner} 승리`);
        }
    }

    onDispose() {
        console.log(`[GameRoom] 룸 ${this.roomId} 삭제됨`);
    }

    private getPlayerNumber(client: Client): number {
        return this.state.players.get(client.sessionId)?.playerNumber ?? 0;
    }
}

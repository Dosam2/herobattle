# HeroBattle Colyseus 서버

시놀로지 NAS Docker에서 실행되는 1:1 PVP 실시간 게임 서버.

- 게임 서버: `ws://mousemoong.synology.me:7777`
- 모니터 대시보드: `http://mousemoong.synology.me:8888/`

## 구조

```
ColyseusServer/
├── src/
│   ├── main.ts              # 서버 엔트리포인트 (게임:7777, 모니터:8888)
│   └── rooms/
│       ├── GameRoom.ts      # 1:1 PVP 게임 룸 로직
│       └── GameState.ts     # 상태 스키마 정의
├── package.json
├── tsconfig.json
├── Dockerfile
├── docker-compose.yml
└── README.md
```

## 로컬 개발

```bash
# 의존성 설치
npm install

# 개발 모드 (파일 변경 시 자동 재시작)
npm run start:dev

# 일반 실행
npm start
```

- 게임 서버: `ws://localhost:7777`
- 모니터: `http://localhost:8888/`
- 헬스체크: `http://localhost:7777/health`

## 시놀로지 NAS Docker 배포

### 방법 1: docker-compose (추천)

```bash
# NAS에 ColyseusServer 폴더 업로드 후:
cd ColyseusServer
docker-compose up -d --build
```

### 방법 2: 시놀로지 Container Manager GUI

1. **Container Manager** 열기
2. **프로젝트** → **생성** → `docker-compose.yml` 선택
3. 빌드 및 실행

### 포트 설정

| 용도 | 컨테이너 포트 | 호스트 포트 |
|------|-------------|------------|
| 게임 서버 (WebSocket) | 7777 | 7777 |
| 모니터 대시보드 (HTTP) | 8888 | 8888 |

## Unity 클라이언트 설정

**ColyseusManager** 컴포넌트의 Inspector:
- `Server Host`: `mousemoong.synology.me`
- `Server Port`: `7777`
- `Room Name`: `game_room`

## 게임 플로우

1. 클라이언트 A → `JoinOrCreate("game_room")` → Player 1 배정
2. 클라이언트 B → `JoinOrCreate("game_room")` → Player 2 배정
3. 서버가 `game_start` 브로드캐스트 → 양쪽 게임 시작
4. 이동/소환/공격 등의 메시지가 서버를 통해 상대에게 릴레이
5. 기지 파괴 → 서버가 승자 결정 → `game_ended` 전송

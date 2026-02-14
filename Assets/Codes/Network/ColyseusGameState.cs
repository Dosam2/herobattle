// ═══════════════════════════════════════════════════════════
// Colyseus 서버 GameState 스키마 (C# 클라이언트용)
// 서버의 GameState.ts와 동일한 구조를 유지해야 함
// ═══════════════════════════════════════════════════════════
using Colyseus.Schema;

/// <summary>
/// 개별 플레이어 상태 (서버 PlayerState 스키마와 매칭)
/// </summary>
public class NetworkPlayerState : Schema
{
    [Type(0, "number")]
    public float playerNumber = 0;

    [Type(1, "number")]
    public float x = 0;

    [Type(2, "number")]
    public float y = 0.5f;

    [Type(3, "number")]
    public float z = 0;

    [Type(4, "number")]
    public float hp = 100;

    [Type(5, "number")]
    public float baseHp = 500;

    [Type(6, "boolean")]
    public bool isReady = false;

    [Type(7, "boolean")]
    public bool isAlive = true;
}

/// <summary>
/// 게임 룸 전체 상태 (서버 GameState 스키마와 매칭)
/// phase: "waiting" → "playing" → "ended"
/// </summary>
public class NetworkGameState : Schema
{
    [Type(0, "map", typeof(MapSchema<NetworkPlayerState>))]
    public MapSchema<NetworkPlayerState> players = new MapSchema<NetworkPlayerState>();

    [Type(1, "string")]
    public string phase = "waiting";

    [Type(2, "number")]
    public float winner = 0;

    [Type(3, "number")]
    public float elapsedTime = 0;
}

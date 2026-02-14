using UnityEngine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// NAS Docker 서버(WebSocket)에 연결해 1:1 매칭 및 메시지 송수신.
/// PUN 없이 자체 서버로 테스트 가능.
/// </summary>
public class NasServerClient : MonoBehaviour
{
    public static NasServerClient Instance { get; private set; }

    [Header("Server (NAS Docker)")]
    [SerializeField] private string serverHost = "192.168.0.10";
    [SerializeField] private int serverPort = 3000;
    [SerializeField] private string roomId = "1v1";

    public bool IsConnected => _ws != null && _ws.State == WebSocketState.Open;
    public int LocalPlayerNumber { get; private set; }
    public bool IsInRoom { get; private set; }

    public event Action OnConnected;
    public event Action<int> OnJoinedRoom;
    public event Action OnGameStart;
    public event Action<string, Dictionary<string, object>> OnMessage;
    public event Action<string> OnError;

    private ClientWebSocket _ws;
    private CancellationTokenSource _cts;
    private readonly ConcurrentQueue<ReceivedMessage> _incoming = new ConcurrentQueue<ReceivedMessage>();

    private struct ReceivedMessage
    {
        public string Type;
        public Dictionary<string, object> Payload;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        while (_incoming.TryDequeue(out var msg))
        {
            try
            {
                if (msg.Type == "connected") OnConnected?.Invoke();
                else if (msg.Type == "joined")
                {
                    if (msg.Payload != null && msg.Payload.TryGetValue("playerNumber", out var num))
                    {
                        LocalPlayerNumber = Convert.ToInt32(num);
                        IsInRoom = true;
                        OnJoinedRoom?.Invoke(LocalPlayerNumber);
                    }
                }
                else if (msg.Type == "game_start") OnGameStart?.Invoke();
                else if (msg.Type == "error" && msg.Payload != null && msg.Payload.TryGetValue("message", out var err))
                    OnError?.Invoke(err.ToString());
                OnMessage?.Invoke(msg.Type, msg.Payload ?? new Dictionary<string, object>());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NasServer] Message handle: {e.Message}");
            }
        }
    }

    /// <summary>서버 주소 설정 (NAS IP 등)</summary>
    public void SetServer(string host, int port)
    {
        serverHost = host;
        serverPort = port;
    }

    public void Connect()
    {
        if (_ws != null && _ws.State == WebSocketState.Open) return;
        _cts = new CancellationTokenSource();
        _ = ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        var uri = new Uri($"ws://{serverHost}:{serverPort}");
        _ws = new ClientWebSocket();
        try
        {
            await _ws.ConnectAsync(uri, _cts.Token);
            _ = ReceiveLoopAsync();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NasServer] Connect failed: {e.Message}");
            Enqueue("error", new Dictionary<string, object> { { "message", e.Message } });
        }
    }

    public void JoinRoom(string id = null)
    {
        roomId = id ?? roomId;
        Send("join", new Dictionary<string, object> { { "roomId", roomId } });
    }

    public void LeaveRoom()
    {
        Send("leave", null);
        IsInRoom = false;
    }

    public void Send(string type, Dictionary<string, object> payload)
    {
        if (!IsConnected) return;
        try
        {
            var obj = new Dictionary<string, object> { { "type", type } };
            if (payload != null) foreach (var kv in payload) obj[kv.Key] = kv.Value;
            var json = JsonUtilityToDict(obj);
            var bytes = Encoding.UTF8.GetBytes(json);
            _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NasServer] Send failed: {e.Message}");
        }
    }

    private async Task ReceiveLoopAsync()
    {
        var buf = new byte[4096];
        var seg = new ArraySegment<byte>(buf);
        try
        {
            while (_ws != null && _ws.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
            {
                var result = await _ws.ReceiveAsync(seg, _cts.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;
                var json = Encoding.UTF8.GetString(buf, 0, result.Count);
                ParseAndEnqueue(json);
            }
        }
        catch (Exception e)
        {
            if (!_cts.Token.IsCancellationRequested)
                Debug.LogWarning($"[NasServer] Receive: {e.Message}");
        }
    }

    private void ParseAndEnqueue(string json)
    {
        try
        {
            var jo = JObject.Parse(json);
            var type = jo["type"]?.ToString() ?? "";
            var payload = new Dictionary<string, object>();
            foreach (var kv in jo)
            {
                if (kv.Key == "type") continue;
                if (kv.Value is JValue jv) payload[kv.Key] = jv.Value;
                else if (kv.Value != null) payload[kv.Key] = kv.Value.ToString();
            }
            _incoming.Enqueue(new ReceivedMessage { Type = type, Payload = payload });
        }
        catch
        {
            _incoming.Enqueue(new ReceivedMessage { Type = "raw", Payload = new Dictionary<string, object> { { "json", json } } });
        }
    }

    private static string JsonUtilityToDict(Dictionary<string, object> d)
    {
        return JsonConvert.SerializeObject(d);
    }

    private void Enqueue(string type, Dictionary<string, object> payload)
    {
        _incoming.Enqueue(new ReceivedMessage { Type = type, Payload = payload });
    }

    public void Disconnect()
    {
        _cts?.Cancel();
        try { _ws?.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); } catch { }
        _ws?.Dispose();
        _ws = null;
        IsInRoom = false;
    }

    private void OnDestroy()
    {
        Disconnect();
        if (Instance == this) Instance = null;
    }

}

using Mirror;
using System.Collections.Generic;

#region 요청 메시지

// 클라이언트가 서버에 "방 리스트 보여줘" 요청할 때 사용
public struct RoomListRequestMessage : NetworkMessage
{
    // 내용 없음 (구조만 필요)
}

#endregion


#region 응답 메시지

// 서버가 클라이언트에게 "이게 방 리스트야" 보낼 때 사용
public struct RoomListSyncMessage : NetworkMessage
{
    public List<RoomInfo> roomList;
}

#endregion


#region 방 정보 구조체

// 각 방에 대한 정보
public struct RoomInfo
{
    public string matchId;         // 내부적으로 사용할 고유 ID
    public string roomName;        // UI에 표시할 이름
    public int currentPlayers;     // 현재 접속한 인원
    public int maxPlayers;         // 최대 인원
}

#endregion

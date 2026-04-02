using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public static class ResultSender
{
    public static IEnumerator SendResultToServer(string playerId, GameResultData result)
    {
        // Tùy server, bạn có thể cần đổi URL này
        string url = "https://localhost.com/5238/api/game-result";
        WWWForm form = new WWWForm();
        form.AddField("playerId", playerId);
        form.AddField("topMajorName", result.topMajorName);
        form.AddField("topMajorPercent", result.topMajorPercent);
        // Nếu muốn gửi thêm, bổ sung ở đây

        using (UnityWebRequest www = UnityWebRequest.Post(url, form))
        {
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"SendResultToServer failed: {www.error}");
            }
            else
            {
                Debug.Log("Result sent to server successfully");
            }
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public static class ResultSender
{
    private const string DefaultEndpoint = "http://localhost:5238/api/game-result";

    public static IEnumerator SendResultToServer(string playerId, GameResultData result)
    {
        // Nếu chưa có backend chạy local, chỉ bỏ qua để không làm nhiễu gameplay.
        string url = DefaultEndpoint;

        if (string.IsNullOrWhiteSpace(url))
        {
            yield break;
        }

        WWWForm form = new WWWForm();
        form.AddField("playerId", playerId);
        form.AddField("finalScore", result.finalScore);

        using (UnityWebRequest www = UnityWebRequest.Post(url, form))
        {
            www.timeout = 5;
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[ResultSender] Skip backend submit: {www.error}");
            }
            else
            {
                Debug.Log("[ResultSender] Result sent to server successfully");
            }
        }
    }
}

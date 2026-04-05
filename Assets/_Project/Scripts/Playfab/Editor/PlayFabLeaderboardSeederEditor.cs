using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayFabLeaderboardSeeder))]
public class PlayFabLeaderboardSeederEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PlayFabLeaderboardSeeder seeder = (PlayFabLeaderboardSeeder)target;
        GUILayout.Space(8f);
        if (GUILayout.Button("Run Seeder"))
        {
            seeder.SeedLeaderboard();
        }
    }
}

using UnityEngine;
using TMPro;

public class FloatingText : MonoBehaviour
{
    public float amplitude = 10f;   // Biên độ dao động (độ cao lên xuống)
    public float frequency = 1f;    // Tốc độ dao động

    private Vector3 startPos;

    void Start()
    {
        // Lưu vị trí ban đầu của chữ
        startPos = transform.localPosition;
    }

    void Update()
    {
        // Tính toán dao động theo trục Y
        float newY = startPos.y + Mathf.Sin(Time.time * frequency) * amplitude;
        transform.localPosition = new Vector3(startPos.x, newY, startPos.z);
    }
}

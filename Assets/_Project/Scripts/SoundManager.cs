using UnityEngine;

public class SoundManager : MonoBehaviour
{
	[Header("BGM")]
	[SerializeField] private AudioSource bgmSource;
	[SerializeField, Range(0f, 1f)] private float normalVolume = 0.8f;
	[SerializeField, Range(0f, 1f)] private float lowVolume = 0.3f;
	[SerializeField] private bool playOnStart = true;

	private void Awake()
	{
		if (bgmSource == null)
		{
			bgmSource = GetComponent<AudioSource>();
		}

		if (bgmSource != null)
		{
			bgmSource.loop = true;
			bgmSource.volume = normalVolume;
		}
	}

	private void Start()
	{
		if (playOnStart)
		{
			PlayBgm();
		}
	}

	public void PlayBgm()
	{
		if (bgmSource == null)
		{
			return;
		}

		bgmSource.volume = normalVolume;
		if (!bgmSource.isPlaying)
		{
			bgmSource.Play();
		}
	}

	public void SetLowVolume()
	{
		if (bgmSource == null)
		{
			return;
		}

		bgmSource.volume = lowVolume;
	}

	public void OnStartButton()
	{
		SetLowVolume();
	}
}

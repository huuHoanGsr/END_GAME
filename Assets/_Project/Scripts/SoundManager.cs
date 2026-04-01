using UnityEngine;

public class SoundManager : MonoBehaviour
{
	private const string BgmVolumeKey = "BgmVolume";

	[Header("BGM")]
	[SerializeField] private AudioClip bgmClip;
	[SerializeField] private AudioSource bgmSource;
	[SerializeField, Range(0f, 1f)] private float normalVolume = 0.8f;
	[SerializeField, Range(0f, 1f)] private float lowVolume = 0.3f;
	[SerializeField] private bool playOnStart = true;

	private bool _isLowVolume;

	private void Awake()
	{
		LoadUserVolume();

		if (bgmSource == null)
		{
			bgmSource = GetComponent<AudioSource>();
		}

		if (bgmSource != null)
		{
			if (bgmSource.clip == null && bgmClip != null)
			{
				bgmSource.clip = bgmClip;
			}

			bgmSource.loop = true;
			bgmSource.volume = normalVolume;
			_isLowVolume = false;
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
		_isLowVolume = false;
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
		_isLowVolume = true;
	}

	public void SetNormalVolume()
	{
		if (bgmSource == null)
		{
			return;
		}

		bgmSource.volume = normalVolume;
		_isLowVolume = false;
	}

	public void SetUserVolume(float volume)
	{
		normalVolume = Mathf.Clamp01(volume);
		PlayerPrefs.SetFloat(BgmVolumeKey, normalVolume);

		if (bgmSource == null)
		{
			return;
		}

		if (!_isLowVolume)
		{
			bgmSource.volume = normalVolume;
		}
	}

	public float GetUserVolume()
	{
		return normalVolume;
	}

	private void LoadUserVolume()
	{
		if (PlayerPrefs.HasKey(BgmVolumeKey))
		{
			normalVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey));
		}
	}

	public void OnStartButton()
	{
		SetLowVolume();
	}
}

using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    private AudioSource audioSource;
    private AudioListener audioListener;
    [SerializeField] private AudioClip mainBGM;
    [SerializeField] private AudioClip gameBGM;
    [SerializeField] private float fadeDuration = 1f; // 페이드 시간(초)
    [SerializeField] private float targetVolume = 1f; // 목표 볼륨

    private Coroutine fadeCoroutine;

    private void Awake()
    {
        // 싱글톤 패턴
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null); // 최상위 객체로 유지
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // AudioSource 초기화
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // AudioListener 초기화
        EnsureAudioListener();

        // AudioSource 설정
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.enabled = true;
        audioSource.volume = 0f; // 초기 볼륨 0
        gameObject.SetActive(true);
    }

    private void Start()
    {
        PlayMainBGM();
    }

    // AudioListener 보장
    private void EnsureAudioListener()
    {
        AudioListener[] listeners = FindObjectsOfType<AudioListener>();
        audioListener = GetComponent<AudioListener>();

        if (listeners.Length == 0 || (listeners.Length == 1 && listeners[0] == audioListener && !audioListener.enabled))
        {
            if (audioListener == null)
            {
                audioListener = gameObject.AddComponent<AudioListener>();
                Debug.Log("[AudioManager] Added new AudioListener to AudioManager");
            }
            audioListener.enabled = true;
        }
        else
        {
            foreach (var listener in listeners)
            {
                if (listener != audioListener)
                {
                    Debug.LogWarning($"[AudioManager] Disabling extra AudioListener on {listener.gameObject.name}");
                    listener.enabled = false;
                }
            }

            if (audioListener == null)
            {
                audioListener = gameObject.AddComponent<AudioListener>();
            }
            audioListener.enabled = true;
        }

    }

    public void PlayMainBGM()
    {
        if (mainBGM == null)
        {
            Debug.LogWarning("[AudioManager] Main BGM 클립이 설정되지 않았습니다.");
            return;
        }

        if (audioSource == null)
        {
            Debug.LogError("[AudioManager] AudioSource가 초기화되지 않았습니다.");
            return;
        }

        EnsureAudioListener();

        if (audioSource.clip != mainBGM || !audioSource.isPlaying)
        {
            StartCoroutine(FadeIn(mainBGM));
        }
    }

    public void PlayGameBGM()
    {
        if (gameBGM == null)
        {
            Debug.LogWarning("[AudioManager] Game BGM 클립이 설정되지 않았습니다.");
            return;
        }

        if (audioSource == null)
        {
            Debug.LogError("[AudioManager] AudioSource가 초기화되지 않았습니다.");
            return;
        }

        EnsureAudioListener();

        if (audioSource.clip != gameBGM || !audioSource.isPlaying)
        {
            StartCoroutine(FadeIn(gameBGM));
        }
    }

    public void PauseBGM()
    {
        if (audioSource != null)
        {
            StartCoroutine(FadeOut());
        }
    }

    public void ResumeBGM()
    {
        if (audioSource != null && audioSource.clip != null)
        {
            EnsureAudioListener();
            StartCoroutine(FadeIn(audioSource.clip));
        }
    }

    public void SetVolume(float volume)
    {
        if (audioSource != null)
        {
            targetVolume = Mathf.Clamp01(volume);
            audioSource.volume = targetVolume;
        }
    }

    // 페이드인 코루틴
    private IEnumerator FadeIn(AudioClip clip)
    {
        // 기존 페이드 코루틴 중지
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        // 새로운 클립 설정
        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.volume = 0f;
        audioSource.Play();

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(0f, targetVolume, timer / fadeDuration);
            yield return null;
        }
        audioSource.volume = targetVolume;

        fadeCoroutine = null;
    }

    // 페이드아웃 코루틴
    private IEnumerator FadeOut()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        float startVolume = audioSource.volume;
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, timer / fadeDuration);
            yield return null;
        }
        audioSource.volume = 0f;
        audioSource.Pause();

        fadeCoroutine = null;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureAudioListener();
        if (!audioSource.isPlaying && audioSource.clip != null)
        {
            Debug.Log("[AudioManager] Music stopped, resuming...");
            StartCoroutine(FadeIn(audioSource.clip));
        }
    }
}
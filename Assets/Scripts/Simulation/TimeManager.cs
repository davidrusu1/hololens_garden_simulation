using System;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    // Singleton pattern to ensure only one instance of TimeManager exists
    public static TimeManager Instance { get; private set; }

    [Header("Time Settings")]
    [Tooltip("How many real-life seconds equal one in-game day?")]
    [SerializeField] private float realSecondsPerDay = 5f; 
    
    [field: Header("Current Time")]
    [SerializeField] public int currentDay { get; private set; } = 1;
    private float timer = 0f;

    public static event Action OnDayChanged;

    private void Awake()
    {
        // Instanciate the singleton instance if it doesn't exist, otherwise destroy the duplicate
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject); 
        }
    }

    private void Update()
    {
        // Time.deltaTime is the time in seconds since the last frame
        timer += Time.deltaTime;

        // If our timer reaches the limit...
        if (timer >= realSecondsPerDay)
        {
            // Reset the timer and add a day
            timer = 0f;
            currentDay++;

            // Log day in a debug message every 5 days
            if(currentDay % 5 == 0)
            {
                Debug.Log($"Day {currentDay}");   
            }

            // Broadcast the signal to the rest of the game
            OnDayChanged?.Invoke(); 
        }
    }

    public void SkipToNextDay()
    {
        timer = 0f;
        currentDay++;
        OnDayChanged?.Invoke();
    }
}


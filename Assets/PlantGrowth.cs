using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlantGrowth : MonoBehaviour
{
    public Slider sunlightSlider;
    public Slider waterSlider;
    public Slider soilSlider;
    public Slider temperatureSlider;
    public TMP_Text temperatureText;
    public TMP_Text sunlightText;
    public TMP_Text waterText;
    public TMP_Text soilgradeText;



    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        float temperature = temperatureSlider.value;
        float sunlight = sunlightSlider.value;
        float water = waterSlider.value;
        float soil = soilSlider.value;

        temperatureText.text = "temperature: \n" + temperature.ToString("0") + " °C";
        sunlightText.text = "sunlight: \n" + sunlight.ToString("0") + " %";
        waterText.text = "water: \n" + water.ToString("0") + " %";
        soilgradeText.text = "soil: \n" + soil.ToString("0") + " %";
    }
}

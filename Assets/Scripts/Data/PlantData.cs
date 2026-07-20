using System.Collections;
using System.Collections.Generic;
using UnityEngine;



[CreateAssetMenu(fileName = "New Plant", menuName = "Garden/Plant Data")]
public class PlantData : ScriptableObject
{
    [Header("Basic information")]
    public string plantName;

    [field: Space(10)]
    [field:Header("Emergence")]
    
    // Temperature parameters for emergence
    [field: SerializeField]
    [field: Tooltip("Temperature sum from sowing to emergence")]
    public float TSUMEM {get; private set;}
    [field: SerializeField]
    [field: Tooltip("Lower threshold temperature for emergence")]
    public float TBASEM {get; private set;}
    [field: SerializeField]
    [field: Tooltip("Maximum effective temperature for emergence")]
    public float TEFFMX {get; private set;}

    [field: Space(10)]
    [field:Header("Phenology (Development)")]

    [field: SerializeField]
    [field: Tooltip("indicates whether pre-anthesis development depends on temperature (0) temperature and daylength (1) temperature, daylength and vernalization (2)")]
    public int IDSL {get; private set;}

    // Daylenght parameters
    [field: SerializeField]
    [field: Tooltip("critical day length for development (lower threshold)")]
    public float DLC {get; private set;}
    [field: SerializeField]
    [field: Tooltip("Optimum day length for development")]
    public float DLO {get; private set;}
    
    [field: SerializeField]
    [field: Tooltip("AFGEN Table: X = Daily Temp, Y = Effective Heat Absorbed")]
    public Vector2[] DTSMTB { get; private set; }

    // Vernalization parameters
    [field: SerializeField]
    [field: Tooltip("Base vernalization requirements ")]
    public float VERNBASE {get; private set;}
    [field: SerializeField]
    [field: Tooltip("Saturated vernalization requirements ")]
    public float VERNSAT {get; private set;}
    [field: SerializeField]
    [field: Tooltip("Vernalization AFGEN Table: X = Daily Temp, Y = Vernalization Rate")]
    public Vector2[] VERNRTB { get; private set; }
    [field: SerializeField]
    [field: Tooltip("DVS after which vernalization will be disabled")]
    public float VERNDVS {get; private set;}

    // Temperature parameters for flowering
    [field: SerializeField]
    [field: Tooltip("Threshold temperature sum from emergence to anthesis (flowering)")]
    public float TSUM1 {get; private set;}

    // Temperature parameters for maturity
    [field: SerializeField]
    [field: Tooltip("Threshold temperature sum from anthesis (flowering) to maturity")]
    public float TSUM2 {get; private set;}

    // Developmnet stages
    [field: SerializeField]
    [field: Tooltip("Development stage at harvest")]
    public float DVSEND {get; private set;}
}

using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class Testingincode : MonoBehaviour
{
    public GameObject cubePrefab;
    public GameObject cylinderPrefab;
    bool timerrunning = false;
    float timer = 0f;
    float growth = 2f;
    bool once = false;

    // Start is called before the first frame update
    void Start()
    {
        timerrunning = true;
    }

    float value = 0f;

    // Update is called once per frame
    void Update()
    {
        bool up = Input.GetKey(KeyCode.UpArrow);
        bool down = Input.GetKey(KeyCode.DownArrow);
        bool space = Input.GetKeyDown(KeyCode.Space);
        float oldValue = value;

        //if (up)
        //{
        //    value += 1f * Time.deltaTime;
        //    //Debug.Log("Value increased to: " + value);

        //}
        //else if (down)
        //{
        //    value -= 1f * Time.deltaTime;
        //    //Debug.Log("Value decreased to: " + value);
        //}

        //value = Mathf.Clamp(value, -50f, 100f);

        if (space)
        {
            timerrunning = !timerrunning;
            if (timerrunning)
                timer = 0f;
        }

        if (timerrunning)
        {
            timer += Time.deltaTime;
            if (timer >= 1f)
            {
                timer = 0f;
                transform.localScale += Vector3.one *2f;
            }
        }

        if (transform.localScale.x >= 20f && !once)
        {
            once = true;
            // Create the cube
            GameObject cube = Instantiate(cubePrefab, transform.position, Quaternion.identity);

            Transform top = cube.transform.Find("Top");

            GameObject newCylinder = Instantiate(cylinderPrefab, top.position, Quaternion.identity);
            newCylinder.transform.localScale = new Vector3(10f, 10f, 10f);

            Debug.Log("Destroying: " + gameObject.name);
            Destroy(gameObject);
        }


    }
}

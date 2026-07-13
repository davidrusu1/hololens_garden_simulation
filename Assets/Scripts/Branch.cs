using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Branch : MonoBehaviour
{

    public GameObject jointPrefab;
    public GameObject branchPrefab;

    public Transform modelCyl;

    public float delayBranches = 1.0f;

    public float vitezaCrestere = 0.5f;
    public float maxHeight = 1.0f;
    public bool maxed = false;
    public bool init = false;
    public bool readyToGrow = false;
    public int currGen = 0;
    public int maxGen = 1;

    void Start()
    {
        if (currGen == 0)
        {
            init = true;
            readyToGrow = true;
        }

        if (currGen > 0)
        {
            foreach (Transform child in this.transform)
            {
                if (child != modelCyl)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        if (modelCyl != null)
        {
            modelCyl.localScale = new Vector3(1f, 0f, 1f);
        }
    }

    void Update()
    {
        Debug.Log("intru aici: " + currGen);
        if (!readyToGrow || !init || modelCyl == null || maxed) return;

        if (modelCyl.localScale.y < maxHeight)
        {
            float crestere = 1 * vitezaCrestere * Time.deltaTime;

            modelCyl.localScale += new Vector3(0f, crestere, 0f);
            modelCyl.localPosition = new Vector3(0f, modelCyl.localScale.y, 0f);
        }
        else
        {
            modelCyl.localScale = new Vector3(1f, maxHeight, 1f);
            modelCyl.localPosition = new Vector3(0f, maxHeight, 0f);
            maxed = true;
            if (currGen < maxGen)
            {
                readyToGrow = true;
                StartCoroutine(GenJoints(2));
            }
        }
    }

    public IEnumerator GenJoints(int nrJoints)
    {
        float realHeight = maxHeight * 2f;

        for (int i = 0; i < nrJoints; i++)
        {
            float randHeight = Random.Range(0.3f, realHeight - 0.3f);
            float randAngle = Random.Range(0f, 360f);

            GameObject newJoint = Instantiate(jointPrefab, this.transform);
            newJoint.transform.localPosition = new Vector3(0f, randHeight, 0f);
            newJoint.transform.localRotation = Quaternion.Euler(45f, randAngle, 0f);
            newJoint.name = "Side_Joint_" + i;

            if (currGen + 1 <= maxGen)
            {

                GameObject newBranch = Instantiate(branchPrefab, newJoint.transform);
                newBranch.transform.localPosition = Vector3.zero;
                newBranch.transform.localRotation = Quaternion.identity;

                Branch newScript = newBranch.GetComponent<Branch>();
                if (newScript != null)
                {
                    newScript.currGen = this.currGen + 1;
                    newScript.maxGen = this.maxGen;
                    newScript.vitezaCrestere = this.vitezaCrestere;
                    newScript.maxed = false;

                    newScript.init = true;
                    while (!newScript.maxed)
                    {
                        yield return null;
                    }
                }
            }

            yield return new WaitForSeconds(delayBranches);
        }
    }
}

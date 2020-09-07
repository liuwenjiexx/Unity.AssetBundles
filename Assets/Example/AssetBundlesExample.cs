using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AssetBundlesExample : MonoBehaviour
{
    // Start is called before the first frame update
    IEnumerator Start()
    {
        yield return AssetBundles.InitializeAsync();

        var task = AssetBundles.InstantiateAsync("Assets/Example/Src/Cube");
        yield return task;
        task.Result.transform.position = Random.insideUnitSphere;

        AssetBundles.InstantiateAsync("Assets/Example/Src/Sphere");
    }

    // Update is called once per frame
    void Update()
    {

    }
}

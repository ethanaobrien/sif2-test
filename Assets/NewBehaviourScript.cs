using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("bundleLoad");
        AssetBundle assetBundle = AssetBundle.LoadFromFile(Application.streamingAssetsPath + "/387b0126300c54515911bffb6540982d.unity3d");
		var names = assetBundle.GetAllAssetNames();
		foreach (var name in names) {
			Debug.Log(name);
		}
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class DropCounter : MonoBehaviour {

    private Text textHolder;
    public string GuiText;

	// Use this for initialization
	void Start () {
        textHolder = GetComponent<Text>();
	}
	
	// Update is called once per frame
	void Update () {
        textHolder.text = GuiText;
	}
}

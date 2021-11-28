using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PickerItem : MonoBehaviour {
    public Image itemImage;
    public GameObject downloadingOverlay;
    public Image borderImage;
    public Button button;

    [NonSerialized]
	public string bundleName;

	public void Initialize(string bundleName) {
        this.bundleName = bundleName;
        StartCoroutine(this.UpdateImage());
	}

	private IEnumerator UpdateImage() {
        this.downloadingOverlay.SetActive(true);
        this.itemImage.gameObject.SetActive(false);

        var result = new RefData<Sprite>();
        yield return StartCoroutine(AssetsManager.Instance.GetThumbnail(this.bundleName, result));

        this.downloadingOverlay.SetActive(false);
        this.itemImage.sprite = result.data;
        this.itemImage.gameObject.SetActive(true);
    }
}

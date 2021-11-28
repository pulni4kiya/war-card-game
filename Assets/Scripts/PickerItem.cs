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

	private bool hasSeparateThumbnail;

	public void Initialize(string bundleName, bool hasSeparateThumbnail) {
        this.bundleName = bundleName;
        this.hasSeparateThumbnail = hasSeparateThumbnail;
        StartCoroutine(this.UpdateImage());
	}

	private IEnumerator UpdateImage() {
        this.downloadingOverlay.SetActive(true);
        this.itemImage.gameObject.SetActive(false);

        var result = new RefData<Sprite>();
        yield return StartCoroutine(AssetsManager.Instance.GetThumbnail(this.bundleName, this.hasSeparateThumbnail, result));

        this.downloadingOverlay.SetActive(false);
        this.itemImage.sprite = result.data;
        this.itemImage.gameObject.SetActive(true);
    }
}

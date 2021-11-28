using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingsController : MonoBehaviour {
    public PickerItem pickerItemPrefab;
    public RectTransform faceItemsContainer;

    private List<PickerItem> faceItems = new List<PickerItem>();
    private PickerItem selectedCardFacesItem;

    private float timeScaleOnEnable;

	private void OnEnable() {
        this.timeScaleOnEnable = Time.timeScale;
        Time.timeScale = 0f;

        this.faceItems.Clear();
        this.faceItemsContainer.ClearChildren();

        var cardFaces = ApplicationSettings.CardFaces;

        var faces = AssetsManager.Instance.assetsInfo.deckFaces;
        foreach (var bundleName in faces) {
            var item = GameObject.Instantiate(this.pickerItemPrefab, this.faceItemsContainer);
            item.Initialize(bundleName);
            faceItems.Add(item);

            if (bundleName == cardFaces) {
                this.selectedCardFacesItem = item;
			}

            item.button.onClick.AddListener(() => {
                this.ChangeSelectedFace(item);
            });
		}

        this.UpdateHighlight();
	}

	private void OnDisable() {
        Time.timeScale = this.timeScaleOnEnable;
	}

	private void ChangeSelectedFace(PickerItem item) {
        this.selectedCardFacesItem = item;
        this.UpdateHighlight();
	}

	private void UpdateHighlight() {
        foreach (var item in this.faceItems) {
            item.borderImage.color = item == this.selectedCardFacesItem ? Color.green : Color.white;
		}
	}

    public void ApplySettings() {
        ApplicationSettings.CardFaces = this.selectedCardFacesItem.bundleName;
	}
}

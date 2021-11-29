using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingsController : MonoBehaviour {
    public PickerItem pickerItemPrefab;
    public RectTransform faceItemsContainer;
    public RectTransform backItemsContainer;
    public RectTransform backgroundItemsContainer;

    private PickerState cardFacePicker;
    private PickerState cardBackPicker;
    private PickerState backgroundPicker;


    private float timeScaleOnEnable;

	private void Awake() {
        this.cardFacePicker = new PickerState() { itemsContainer = this.faceItemsContainer, pickerItemPrefab = this.pickerItemPrefab };
        this.cardBackPicker = new PickerState() { itemsContainer = this.backItemsContainer, pickerItemPrefab = this.pickerItemPrefab };
        this.backgroundPicker = new PickerState() { itemsContainer = this.backgroundItemsContainer, pickerItemPrefab = this.pickerItemPrefab };
    }

	private void OnEnable() {
        this.timeScaleOnEnable = Time.timeScale;
        Time.timeScale = 0f;

        this.cardFacePicker.Initialize(AssetsManager.Instance.assetsInfo.cardFaceBundles, ApplicationSettings.CardFaces, true);
        this.cardBackPicker.Initialize(AssetsManager.Instance.assetsInfo.cardBackBundles, ApplicationSettings.CardBacks, false);
        this.backgroundPicker.Initialize(AssetsManager.Instance.assetsInfo.backgroundBundles, ApplicationSettings.Background, false);
    }

	private void OnDisable() {
        Time.timeScale = this.timeScaleOnEnable;
	}

    public void ApplySettings() {
        ApplicationSettings.CardFaces = this.cardFacePicker.selectedItem.bundleName;
        ApplicationSettings.CardBacks = this.cardBackPicker.selectedItem.bundleName;
        ApplicationSettings.Background = this.backgroundPicker.selectedItem.bundleName;
        ApplicationSettings.RaiseChangedEvent();
    }

    // Normally this would be a stand-alone UI control, buuut it's getting late
    public class PickerState {
        public PickerItem pickerItemPrefab;
        public RectTransform itemsContainer;
        public List<PickerItem> items = new List<PickerItem>();
        public PickerItem selectedItem;

        public void Initialize(List<string> bundles, string selected, bool hasSeparateThumbnail) {
            this.items.Clear();
            this.itemsContainer.ClearChildren();

            foreach (var bundleName in bundles) {
                var item = GameObject.Instantiate(this.pickerItemPrefab, this.itemsContainer);
                item.Initialize(bundleName, hasSeparateThumbnail);
                items.Add(item);

                if (bundleName == selected) {
                    this.selectedItem = item;
                }

                item.button.onClick.AddListener(() => {
                    this.ChangeSelection(item);
                });
            }

            this.UpdateHighlight();
        }

        private void ChangeSelection(PickerItem item) {
            this.selectedItem = item;
            this.UpdateHighlight();
        }

        private void UpdateHighlight() {
            foreach (var item in this.items) {
                item.borderImage.color = item == this.selectedItem ? Color.green : Color.white;
            }
        }
    }
}

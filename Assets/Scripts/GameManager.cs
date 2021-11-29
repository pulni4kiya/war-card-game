﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour {
	private static readonly Quaternion FaceUp = Quaternion.identity;
	private static readonly Quaternion FaceDown = Quaternion.Euler(0f, 180f, 0f);

	public int playerCount = 2;
	public Card cardPrefab;
	public PlayerPanel playerPanelPrefab;
	public RectTransform playArea;
	public Button nextRoundButton;
	public float cardAnimationTimeBase;
	public Slider animationSpeedSlider;
	public Toggle autoPlayToggle;
	public Slider waitTimeSlider;
	public Camera mainCamera;
	public GameObject loadingAssetsPanel;
	public Image backgroundImage;
	public RectTransform warLabelTransform;
	public TMP_Text winLossLabel;
	public Sounds sounds;
	public List<CardRank> cardPowerOrder;

	//private WarGameRulesSO rules;

	private GameState state;

	private List<Card> allCards;

	private bool moveToNextRound;

	private float animationSpeed = 1f;

	private float cardWidth;

	private AudioSource audioSource;

	private float animationTime {
		get {
			return this.cardAnimationTimeBase / this.animationSpeed;
		}
	}

	private IEnumerator Start() {
		this.audioSource = this.gameObject.AddComponent<AudioSource>();

		ApplicationSettings.Changed += this.ApplicationSettings_Changed;

		StartCoroutine(AssetsManager.Instance.UpdateAssetsInfo());

		this.cardWidth = ((RectTransform)this.cardPrefab.transform).sizeDelta.x;
		this.InitializeDeck();
		this.InitializeEventHandlers();

		yield return StartCoroutine(this.LoadAsset());

		StartCoroutine(this.PrepareAndPlayGame());
	}

	private void OnDestroy() {
		ApplicationSettings.Changed -= ApplicationSettings_Changed;
	}

	public void NewGame() {
		StopAllCoroutines();
		StartCoroutine(this.PrepareAndPlayGame());
	}

	private void InitializeDeck() {
		this.allCards = new List<Card>(52);

		foreach (var suit in Enum.GetValues(typeof(CardSuit)).Cast<CardSuit>()) {
			foreach (var rank in Enum.GetValues(typeof(CardRank)).Cast<CardRank>()) {
				var card = GameObject.Instantiate(this.cardPrefab, Vector3.zero, Quaternion.identity, this.playArea);
				card.rank = rank;
				card.suit = suit;
				card.gameObject.SetActive(false);
				this.allCards.Add(card);
			}
		}
	}

	private void InitializeEventHandlers() {
		this.nextRoundButton.onClick.AddListener(() => {
			this.moveToNextRound = true;
		});

		this.animationSpeedSlider.onValueChanged.AddListener((value) => {
			Time.timeScale = value;
		});
	}

	private IEnumerator PrepareAndPlayGame() {
		while (true) {
			this.InitializeNewGame();
			yield return null;
			yield return StartCoroutine(this.DealCards());
			yield return StartCoroutine(this.PlayGame());
			yield return StartCoroutine(this.WaitForInput(true));
		}
	}

	private void InitializeNewGame() {
		foreach (var panel in this.playArea.GetComponentsInChildren<PlayerPanel>()) {
			GameObject.Destroy(panel.gameObject);
		}
		this.state = new GameState();

		this.winLossLabel.gameObject.SetActive(false);

		for (int i = 0; i < this.playerCount; i++) {
			var player = new Player();
			player.isComputer = i != 0;

			var panel = GameObject.Instantiate(this.playerPanelPrefab, this.playArea);
			panel.nameLabel.text = i == 0 ? "Player" : "Computer " + i;
			player.panel = panel;

			this.state.players.Add(player);
		}

		foreach (var card in this.allCards) {
			card.power = this.cardPowerOrder.IndexOf(card.rank);
		}
	}

	private IEnumerator DealCards() {
		var activeCards = this.allCards.Where(c => c.power != -1).ToList();
		activeCards.Shuffle();

		for (int i = 0; i < activeCards.Count; i++) {
			var card = activeCards[i];
			var player = this.state.players[i % this.state.players.Count];
			player.cards.Enqueue(card);
			card.transform.localPosition = this.GetPlayerCardPosition(player);
			card.transform.localRotation = FaceDown;
			card.gameObject.SetActive(true);
		}

		foreach (var player in this.state.players) {
			player.UpdateCardsCount();
		}

		yield break; // Method designed for nice animation that will be made later
	}

	private IEnumerator PlayGame() {
		while (this.state.gameWinner == null) {
			yield return StartCoroutine(this.PlayOneRound());
			this.DetermineGameWinner();
		}
		if (this.state.gameWinner.isComputer == false) {
			this.PlaySound(this.sounds.win);
			this.winLossLabel.text = "WIN!";
			this.winLossLabel.color = Color.green;
		} else {
			this.PlaySound(this.sounds.lose);
			this.winLossLabel.text = "LOSS!";
			this.winLossLabel.color = Color.red;
		}
		this.winLossLabel.gameObject.SetActive(true);
	}

	private IEnumerator WaitForInput(bool mustBeManual) {
		var autoPlayTime = Time.unscaledTime + this.waitTimeSlider.value;
		this.moveToNextRound = false;

		while (true) {
			if (this.moveToNextRound == true) {
				break;
			}

			if (mustBeManual == false) {
				if (this.autoPlayToggle.isOn && Time.unscaledTime >= autoPlayTime) {
					break;
				}
			}

			yield return null;
		}
	}

	private IEnumerator PlayOneRound() {
		this.state.currentRoundCardsOffset = 0;
		this.state.currentRoundCards.Clear();

		foreach (var player in this.state.players) {
			if (player.isActiveInGame == false) {
				continue;
			}

			player.isActiveInRound = true;
		}

		var cardsToDrawBase = 1;
		var warTextScale = 0.5f;
		do {
			var cardsToDraw = cardsToDrawBase;
			if (cardsToDrawBase > 1) {
				var maxRemainingCards = cardsToDrawBase == 1 ? 1 : this.state.players.Where(p => p.isActive).Max(p => p.cards.Count);
				cardsToDraw = Mathf.Min(maxRemainingCards, cardsToDrawBase);
			}
			yield return StartCoroutine(this.DrawCards(cardsToDraw));
			this.DetermineRoundWinners();
			cardsToDrawBase = 3;
			warTextScale += 0.5f;

			if (this.state.currentRoundWinners.Count > 1) {
				this.PlaySound(this.sounds.war);
				yield return StartCoroutine(this.AnimateWarText(warTextScale, this.animationTime, true));
			}
		} while (this.state.currentRoundWinners.Count > 1);

		yield return StartCoroutine(this.WaitForInput(false));

		StartCoroutine(this.AnimateWarText(0f, this.animationTime, false));
		yield return StartCoroutine(this.GiveRoundCardsToPlayer(this.state.currentRoundWinners[0]));
	}

	private void DetermineRoundWinners() {
		this.state.currentRoundWinners.Clear();

		var highestCard = -1;
		foreach (var player in this.state.players) {
			if (player.isActive == false) {
				continue;
			}

			var activeCard = player.activeRoundCard;
			if (player.activeRoundCard.power == highestCard) {
				this.state.currentRoundWinners.Add(player);
			}
			if (player.activeRoundCard.power > highestCard) {
				this.state.currentRoundWinners.Clear();
				this.state.currentRoundWinners.Add(player);
				highestCard = player.activeRoundCard.power;
			}
		}

		foreach (var player in this.state.players) {
			if (player.isActive == false) {
				continue;
			}

			if (player.activeRoundCard.power < highestCard) {
				player.isActiveInRound = false;
			}
		}
	}

	private void DetermineGameWinner() {
		var activePlayers = 0;
		Player winner = null;
		foreach (var player in this.state.players) {
			if (player.isActiveInGame == false) {
				continue;
			}

			if (player.cards.Count == 0) {
				player.isActiveInGame = false;
				continue;
			}

			winner = player;
			activePlayers++;
		}

		if (activePlayers == 1) {
			this.state.gameWinner = winner;
		}
	}

	private IEnumerator DrawCards(int count) {
		for (int i = 0; i < count; i++) {
			foreach (var player in this.state.players) {
				if (player.isActive == false) {
					continue;
				}

				if (player.cards.Count == 0) {
					player.isActiveInRound = false;
					continue;
				}

				var isVisible = i == count - 1 || player.cards.Count == 1;
				StartCoroutine(this.DrawCard(player, isVisible, out var card));
				this.state.currentRoundCards.Add(card);
				if (isVisible) {
					player.activeRoundCard = card;
				}
			}

			this.state.currentRoundCardsOffset++;

			this.PlaySound(this.sounds.cardDraw);

			yield return new WaitForSeconds(this.animationTime);
		}
	}

	private IEnumerator DrawCard(Player player, bool isVisible, out Card card) {
		card = player.cards.Dequeue();

		player.UpdateCardsCount();

		var rotation = isVisible ? FaceUp : FaceDown;
		return this.AnimatePositionAndRotation(card, this.GetPlayerCardPosition(player) + this.GetCardOffset(this.state.currentRoundCardsOffset), rotation, this.animationTime);
	}

	private IEnumerator GiveRoundCardsToPlayer(Player winner) {
		foreach (var card in this.state.currentRoundCards) {
			StartCoroutine(this.AnimatePositionAndRotation(card, this.GetPlayerCardPosition(winner), FaceDown, this.animationTime));
			winner.cards.Enqueue(card);
		}
		winner.UpdateCardsCount();
		yield return new WaitForSeconds(this.animationTime);
	}

	private IEnumerator AnimatePositionAndRotation(Card card, Vector3 targetPosition, Quaternion targetRotation, float time) {
		var startPosition = card.transform.localPosition;
		var startRotation = card.transform.localRotation;

		yield return StartCoroutine(AnimatePositionAndRotation(card, startPosition, targetPosition, startRotation, targetRotation, time));
	}

	private IEnumerator AnimatePositionAndRotation(Card card, Vector3 initialPosition, Vector3 targetPosition, Quaternion initialRotaion, Quaternion targetRotation, float time) {
		card.transform.localPosition = initialPosition;
		card.transform.localRotation = initialRotaion;

		var dt = 0f;
		while (dt < time) {
			dt += Time.deltaTime;
			var t = dt / time;
			card.transform.localPosition = Vector3.Lerp(initialPosition, targetPosition, t);
			card.transform.localRotation = Quaternion.Lerp(initialRotaion, targetRotation, t);
			yield return null;
		}
	}

	private Vector3 GetCardOffset(int offsetPositions) {
		var horizontal = Vector3.right * ((1.3f + offsetPositions * 0.2f) * this.cardWidth);
		var depth = Vector3.back * (offsetPositions * 0.01f);
		return horizontal + depth;
	}

	private void UpdateCardVisuals(Card card) {
		card.faceImage.sprite = this.GetCardSprite(card.suit, card.rank);
		card.backImage.sprite = AssetsManager.Instance.GetCardBackSprite();
	}

	private Sprite GetCardSprite(CardSuit suit, CardRank rank) {
		return AssetsManager.Instance.GetCardSprite(suit, rank);
	}

	private Vector3 GetPlayerCardPosition(Player player) {
		return player.panel.cardReferencePoint.TransformPointTo(this.playArea, Vector3.zero);
	}

	private IEnumerator LoadAsset() {
		this.loadingAssetsPanel.SetActive(true);

		yield return StartCoroutine(AssetsManager.Instance.LoadCardsPack(ApplicationSettings.CardFaces));
		yield return StartCoroutine(AssetsManager.Instance.LoadCardBack(ApplicationSettings.CardBacks));
		yield return StartCoroutine(AssetsManager.Instance.LoadBackground(ApplicationSettings.Background));

		foreach (var card in this.allCards) {
			this.UpdateCardVisuals(card);
		}

		this.backgroundImage.sprite = AssetsManager.Instance.GetBackgroundSprite();

		this.loadingAssetsPanel.SetActive(false);
	}

	private void ApplicationSettings_Changed(object sender, EventArgs e) {
		this.StartCoroutine(this.LoadAsset());
	}

	private IEnumerator AnimateWarText(float newScale, float time, bool shake) {
		var fromScale = this.warLabelTransform.localScale;
		var toScale = Vector3.one * newScale;

		var dt = 0f;
		while (dt < time) {
			dt += Time.deltaTime;
			var t = dt / time;
			this.warLabelTransform.localScale = Vector3.Lerp(fromScale, toScale, t);
			if (shake) {
				this.warLabelTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.PerlinNoise(dt * 100f, 0f) * 50 - 25f);
			}
			yield return null;
		}

		this.warLabelTransform.localRotation = Quaternion.identity;
	}

	private void PlaySound(AudioClip sound) {
		this.audioSource.PlayOneShot(sound);
	}

	[Serializable]
	public class Sounds {
		public AudioClip cardDraw;
		public AudioClip war;
		public AudioClip win;
		public AudioClip lose;
	}
}

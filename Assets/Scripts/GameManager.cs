﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour {
	private static readonly Quaternion FaceUpRotation = Quaternion.identity;
	private static readonly Quaternion FaceDownRotation = Quaternion.Euler(0f, 180f, 0f);

	[SerializeField] private int playerCount = 2;
	[SerializeField] private Card cardPrefab;
	[SerializeField] private PlayerPanel playerPanelPrefab;
	[SerializeField] private RectTransform playArea;
	[SerializeField] private Button nextRoundButton;
	[SerializeField] private float cardAnimationTimeBase;
	[SerializeField] private Slider animationSpeedSlider;
	[SerializeField] private Toggle autoPlayToggle;
	[SerializeField] private Slider waitTimeSlider;
	[SerializeField] private Camera mainCamera;
	[SerializeField] private GameObject loadingAssetsPanel;
	[SerializeField] private Image backgroundImage;
	[SerializeField] private RectTransform warLabelTransform;
	[SerializeField] private TMP_Text winLossLabel;
	[SerializeField] private Sounds sounds;
	[SerializeField] private List<CardRank> cardPowerOrder;

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

		this.InitializeUIEventHandlers();

		yield return StartCoroutine(this.LoadAssets());

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

	private void InitializeUIEventHandlers() {
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
		// Show card dealing animation
		yield return StartCoroutine(this.DealCardsAnimation());

		// Actually randomize the cards between the players
		var activeCards = this.allCards.Where(c => c.power != -1).ToList();
		activeCards.Shuffle();

		for (int i = 0; i < activeCards.Count; i++) {
			var card = activeCards[i];
			var player = this.state.players[i % this.state.players.Count];
			player.cards.Enqueue(card);
			card.transform.localPosition = this.GetPlayerCardPosition(player);
			card.transform.localRotation = FaceDownRotation;
			card.gameObject.SetActive(true);
		}

		foreach (var player in this.state.players) {
			player.UpdateCardsCount();
		}
	}

	private IEnumerator DealCardsAnimation() {
		var numberOfCardsPerDraw = 3;

		// Throw cards randomly
		var cardIndex = 0;
		for (int i = 0; i < 2; i++) {
			foreach (var player in this.state.players) {
				yield return StartCoroutine(this.DealCardsToPlayerAnimation(player, cardIndex, numberOfCardsPerDraw));
				cardIndex += numberOfCardsPerDraw;
			}
		}

		// Stack cards
		cardIndex = 0;
		for (int i = 0; i < 2; i++) {
			foreach (var player in this.state.players) {
				for (int j = 0; j < numberOfCardsPerDraw; j++) {
					var card = this.allCards[cardIndex];
					StartCoroutine(this.AnimatePositionAndRotation(card, this.GetPlayerCardPosition(player), FaceDownRotation, this.animationTime));
					cardIndex++;
				}
			}
		}
		yield return new WaitForSeconds(this.animationTime);
	}

	private IEnumerator DealCardsToPlayerAnimation(Player player, int startingIndex, int amount) {
		this.PlaySound(this.sounds.cardDraw);

		for (int i = 0; i < amount; i++) {
			var card = this.allCards[startingIndex + i];
			card.gameObject.SetActive(true);

			var position = this.GetPlayerCardPosition(player) + (Vector3)UnityEngine.Random.insideUnitCircle * (this.cardWidth * 0.3f) + Vector3.back * (startingIndex + i) * 0.01f;
			var rotation = Quaternion.Euler(0f, 180f, UnityEngine.Random.Range(0f, 360f));
			StartCoroutine(this.AnimatePositionAndRotation(card, Vector3.zero, position, FaceDownRotation, rotation, this.animationTime));
		}

		yield return new WaitForSeconds(this.animationTime);
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

	private IEnumerator PlayOneRound() {
		this.PrepareForRound();

		var cardsToDrawBase = 1;
		var warTextScale = 0.5f;
		do {
			// Draw cards
			var cardsToDraw = cardsToDrawBase;
			if (cardsToDrawBase > 1) {
				var maxRemainingCards = cardsToDrawBase == 1 ? 1 : this.state.players.Where(p => p.isActive).Max(p => p.cards.Count);
				cardsToDraw = Mathf.Min(maxRemainingCards, cardsToDrawBase);
			}
			yield return StartCoroutine(this.DrawCards(cardsToDraw));

			// Determine round winner(s)
			this.DetermineRoundWinners();

			// Adjust state for next round (if there's a war)
			cardsToDrawBase = 3;
			warTextScale += 0.5f;

			// Play war effects
			if (this.state.currentRoundWinners.Count > 1) {
				this.PlaySound(this.sounds.war);
				yield return StartCoroutine(this.AnimateWarText(warTextScale, this.animationTime, true));
			}
		} while (this.state.currentRoundWinners.Count > 1);

		yield return StartCoroutine(this.WaitForInput(false));

		StartCoroutine(this.AnimateWarText(0f, this.animationTime, false));

		yield return StartCoroutine(this.GiveRoundCardsToPlayer(this.state.currentRoundWinners[0]));
	}

	private void PrepareForRound() {
		this.state.currentRoundCardsOffset = 0;
		this.state.currentRoundCards.Clear();

		foreach (var player in this.state.players) {
			if (player.isActiveInGame == false) {
				continue;
			}

			player.isActiveInRound = true;
		}
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

		var rotation = isVisible ? FaceUpRotation : FaceDownRotation;
		return this.AnimatePositionAndRotation(card, this.GetPlayerCardPosition(player) + this.GetCardOffset(this.state.currentRoundCardsOffset), rotation, this.animationTime);
	}

	private IEnumerator GiveRoundCardsToPlayer(Player winner) {
		foreach (var card in this.state.currentRoundCards) {
			StartCoroutine(this.AnimatePositionAndRotation(card, this.GetPlayerCardPosition(winner), FaceDownRotation, this.animationTime));
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

	private IEnumerator LoadAssets() {
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
		this.StartCoroutine(this.LoadAssets());
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour {
    private static readonly Quaternion FaceUp = Quaternion.identity;
    private static readonly Quaternion FaceDown = Quaternion.Euler(0f, 180f, 0f);

    public Card cardPrefab;
    public Button nextRoundButton;
    public float cardAnimationTimeBase;
    public Slider animationSpeedSlider;
    public Toggle autoPlayToggle;
    public Slider waitTimeSlider;
    public Camera mainCamera;
    public List<CardRank> cardPowerOrder;

    //private WarGameRulesSO rules;

    private GameState state;

    private List<Card> allCards;

    private bool moveToNextRound;

    private float animationSpeed = 1f;

    private float animationTime {
        get {
            return this.cardAnimationTimeBase / this.animationSpeed;
		}
	}

    private void Start() {
        this.InitializeDeck();
        this.InitializeEventHandlers();
        StartCoroutine(this.PlayGame());
    }

	private void Update() {
        this.UpdateCameraPosition();
	}

	private void UpdateCameraPosition() {
        // Some magic numbers, fix later
        var widthWorldUnits = ((float)Screen.width / Screen.height) * this.mainCamera.orthographicSize * 2f;
        this.mainCamera.transform.position = new Vector3(widthWorldUnits / 2f - 2f, 0f, this.mainCamera.transform.position.z);
	}

	private void InitializeDeck() {
        this.allCards = new List<Card>(52);

        foreach (var suit in Enum.GetValues(typeof(CardSuit)).Cast<CardSuit>()) {
            foreach (var rank in Enum.GetValues(typeof(CardRank)).Cast<CardRank>()) {
                var card = GameObject.Instantiate(this.cardPrefab, Vector3.zero, Quaternion.identity);
                card.rank = rank;
                card.suit = suit;
                card.gameObject.SetActive(false);
                this.UpdateCardVisuals(card);
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

    private IEnumerator PlayGame() {
        this.InitializeNewGameState();
        yield return StartCoroutine(this.DealCards());
        yield return StartCoroutine(this.PlayActualGame());
    }

	private void InitializeNewGameState() {
        this.state = new GameState();

        this.state.players.Add(new Player() { cardsPosition = new Vector3(0f, 3f, 0f) });
        this.state.players.Add(new Player() { cardsPosition = new Vector3(0f, -3f, 0f) });

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
            card.transform.position = player.cardsPosition;
            card.transform.rotation = FaceDown;
            card.gameObject.SetActive(true);
		}

        yield break;
    }

    private IEnumerator PlayActualGame() {
        while (this.state.gameWinner == null) {
            yield return StartCoroutine(this.PlayOneRound());
            this.DetermineGameWinner();
        }
    }

	private IEnumerator WaitForInput() {
        var autoPlayTime = Time.unscaledTime + this.waitTimeSlider.value;
        this.moveToNextRound = false;
        while (this.moveToNextRound == false && (this.autoPlayToggle.isOn == false || Time.unscaledTime < autoPlayTime)) {
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

        int cardsToDraw = 1;
        do {
            yield return StartCoroutine(this.DrawCards(cardsToDraw));
            this.DetermineRoundWinners();
            cardsToDraw = 3;
        } while (this.state.currentRoundWinners.Count > 1);

        yield return StartCoroutine(this.WaitForInput());

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

                var isVisible = i == count - 1 || player.cards.Count == 1;
                StartCoroutine(this.DrawCard(player, isVisible, out var card));
                this.state.currentRoundCards.Add(card);
                if (isVisible) {
                    player.activeRoundCard = card;
				}
            }

            this.state.currentRoundCardsOffset++;

            yield return new WaitForSeconds(this.animationTime);
        }
	}

	private IEnumerator DrawCard(Player player, bool isVisible, out Card card) {
        card = player.cards.Dequeue();
        var rotation = isVisible ? FaceUp : FaceDown;
        return this.AnimatePositionAndRotation(card, player.cardsPosition + this.GetCardOffset(this.state.currentRoundCardsOffset), rotation, this.animationTime);
	}

    private IEnumerator GiveRoundCardsToPlayer(Player winner) {
        foreach (var card in this.state.currentRoundCards) {
            StartCoroutine(this.AnimatePositionAndRotation(card, winner.cardsPosition, FaceDown, this.animationTime));
            winner.cards.Enqueue(card);
		}
        yield return new WaitForSeconds(this.animationTime);
    }

    private IEnumerator AnimatePositionAndRotation(Card card, Vector3 targetPosition, Quaternion targetRotation, float time) {
        var startPosition = card.transform.position;
        var startRotation = card.transform.rotation;

        yield return StartCoroutine(AnimatePositionAndRotation(card, startPosition, targetPosition, startRotation, targetRotation, time));
    }

    private IEnumerator AnimatePositionAndRotation(Card card, Vector3 initialPosition, Vector3 targetPosition, Quaternion initialRotaion, Quaternion targetRotation, float time) {
        card.transform.position = initialPosition;
        card.transform.rotation = initialRotaion;

        var dt = 0f;
        while (dt < time) {
            dt += Time.deltaTime;
            var t = dt / time;
            card.transform.position = Vector3.Lerp(initialPosition, targetPosition, t);
            card.transform.rotation = Quaternion.Lerp(initialRotaion, targetRotation, t);
            yield return null;
        }
    }

    private Vector3 GetCardOffset(int offsetPositions) {
        return Vector3.right * (3f + offsetPositions * 0.5f) + Vector3.back * (offsetPositions * 0.01f);
    }

    private void UpdateCardVisuals(Card card) {
        card.faceImage.sprite = this.GetCardSprite(card.suit, card.rank);
    }

    private Sprite GetCardSprite(CardSuit suit, CardRank rank) {
        return Resources.Load<Sprite>($"Deck1/{suit}_{(int)rank}");
	}
}


using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// In-match HUD: lobby join slots, wave / players-left status, event timers, a card per player
// (health, dash, power-up) and a big cartoon announcer.
public class MatchHud : MonoBehaviour
{
    private static MatchHud instance;

    private RectTransform lobby;
    private TextMeshProUGUI lobbyStatus;
    private readonly List<Image> lobbySlots = new();
    private readonly List<TextMeshProUGUI> lobbySlotLabels = new();
    private RectTransform status;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI eventsText;
    private RectTransform cardRow;
    private const float CardWidth = 380f;
    private const float CardSpacing = 30f;
    private readonly List<PlayerCard> cards = new();
    private TextMeshProUGUI announcer;
    private float announceTimer;
    private float announceDuration;

    private class PlayerCard
    {
        public PlayerInput Player;
        public LocalHealth Health;
        public PlayerDash Dash;
        public PowerUpReceiver PowerUps;
        public float MaxHealth;
        public RectTransform Root;
        public Image Body;
        public RectTransform HealthFill;
        public RectTransform DashFill;
        public TextMeshProUGUI Name;
        public TextMeshProUGUI Note;
    }

    public static MatchHud Create()
    {
        UIFactory.EnsureEventSystem();
        var canvas = UIFactory.CreateCanvas("Match HUD", 10);
        instance = canvas.gameObject.AddComponent<MatchHud>();
        instance.Build(canvas.transform);
        return instance;
    }

    public static void Announce(string text, Color color, float duration = 1f)
    {
        if (instance == null)
            return;
        instance.announcer.text = text;
        instance.announcer.color = color;
        instance.announceTimer = instance.announceDuration = duration;
        instance.announcer.transform.localScale = Vector3.one * 1.6f;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(Transform root)
    {
        // Lobby: join slots.
        lobby = UIFactory.Panel("Lobby", root, new Vector2(1100f, 380f), LabPalette.PanelWhite);
        UIFactory.Place(lobby, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1100f, 380f));
        var title = UIFactory.Label("Title", lobby, MatchSettings.Mode == GameMode.PvP ? "PVP: FREE-FOR-ALL" : "CO-OP: SURVIVE THE TURRETS", 50f, LabPalette.Ink, TextAlignmentOptions.Center, 0f);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1000f, 70f));
        var slots = UIFactory.Rect("Slots", lobby);
        UIFactory.Place(slots, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(980f, 130f));
        var row = slots.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 24f;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = row.childControlHeight = false;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        for (int i = 0; i < MatchSettings.MaxPlayers; i++)
        {
            var slot = UIFactory.Image($"Slot {i + 1}", slots, LabPalette.PanelGrey, RoundedSprite.Get(26));
            slot.rectTransform.sizeDelta = new Vector2(220f, 120f);
            var label = UIFactory.Label("Label", slot.transform, $"P{i + 1}\n<size=24>OPEN</size>", 46f, LabPalette.Ink, TextAlignmentOptions.Center, 0f);
            UIFactory.Stretch(label.rectTransform);
            lobbySlots.Add(slot);
            lobbySlotLabels.Add(label);
        }
        lobbyStatus = UIFactory.Label("Status", lobby, "", 34f, LabPalette.Ink, TextAlignmentOptions.Center, 0f);
        UIFactory.Place(lobbyStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1000f, 90f));
        UIFactory.Motion(lobbyStatus, UIMotion.Kind.Pulse);

        // Way back to the menu (e.g. PvP with only one player). Clickable, never auto-selected,
        // so Enter keeps meaning "join" for keyboard players.
        var style = LabStyle.Active;
        System.Action leave = () =>
        {
            if (MatchDirector.Instance != null)
                MatchDirector.Instance.LeaveLobby();
        };
        CartoonButton exit = style != null && style.exitButton.IsValid
            ? UIFactory.ArtButton("Exit", lobby, style.exitButton, 110f, leave)
            : UIFactory.Button("Exit", lobby, "MAIN MENU", LabPalette.NeonPink, leave, new Vector2(320f, 90f), 38f);
        var exitRect = (RectTransform)exit.transform;
        UIFactory.Place(exitRect, new Vector2(0.5f, 0f), new Vector2(0f, -140f), exitRect.sizeDelta);
        var exitHint = UIFactory.Label("Exit Hint", lobby, "ESC / SELECT: BACK TO MENU", 26f, LabPalette.PanelWhite, TextAlignmentOptions.Center, 0.25f);
        UIFactory.Place(exitHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -186f), new Vector2(700f, 40f));

        // Status pill, top centre.
        status = UIFactory.Panel("Status", root, new Vector2(620f, 92f), LabPalette.PanelWhite, 26, 5f);
        UIFactory.Place(status, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(620f, 92f));
        statusText = UIFactory.Label("Text", status, "", 40f, LabPalette.Ink, TextAlignmentOptions.Center, 0f);
        UIFactory.Stretch(statusText.rectTransform);
        eventsText = UIFactory.Label("Events", root, "", 28f, LabPalette.PanelWhite, TextAlignmentOptions.TopRight, 0.25f);
        UIFactory.Place(eventsText.rectTransform, new Vector2(1f, 1f), new Vector2(-36f, -30f), new Vector2(520f, 120f));

        cardRow = UIFactory.Rect("Players", root);
        UIFactory.Place(cardRow, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1800f, 170f));
        var cardLayout = cardRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        cardLayout.spacing = CardSpacing;
        cardLayout.childAlignment = TextAnchor.LowerCenter;
        cardLayout.childControlWidth = cardLayout.childControlHeight = false;
        cardLayout.childForceExpandWidth = cardLayout.childForceExpandHeight = false;

        announcer = UIFactory.Label("Announcer", root, "", 150f, LabPalette.NeonPink, TextAlignmentOptions.Center, 0.25f);
        UIFactory.Place(announcer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1800f, 240f));
    }

    private void Update()
    {
        var director = MatchDirector.Instance;
        if (director == null)
            return;

        bool inLobby = director.Current == MatchDirector.Phase.Lobby;
        lobby.gameObject.SetActive(inLobby);
        status.gameObject.SetActive(!inLobby);
        if (inLobby)
            UpdateLobby(director);
        else
            UpdateStatus(director);

        SyncCards(director.Players);
        PositionHealthCards();
        foreach (var card in cards)
            UpdateCard(card);

        // Announcer: punch in, hold, fade.
        if (announceTimer > 0f)
        {
            announceTimer -= Time.unscaledDeltaTime;
            float k = 1f - announceTimer / Mathf.Max(0.01f, announceDuration);
            announcer.transform.localScale = Vector3.Lerp(announcer.transform.localScale, Vector3.one, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
            announcer.alpha = k > 0.75f ? Mathf.InverseLerp(1f, 0.75f, k) : 1f;
        }
        else
            announcer.alpha = 0f;
    }

    private void UpdateLobby(MatchDirector director)
    {
        var players = director.Players;
        for (int i = 0; i < lobbySlots.Count; i++)
        {
            bool joined = i < players.Count;
            lobbySlots[i].color = joined ? LabPalette.Player(i) : LabPalette.PanelGrey;
            string device = joined && players[i].currentControlScheme != null ? players[i].currentControlScheme.ToUpperInvariant() : "OPEN";
            lobbySlotLabels[i].text = $"P{i + 1}\n<size=24>{(joined ? device : "OPEN")}</size>";
        }
        int need = MatchSettings.MinPlayers - players.Count;
        if (players.Count == 0)
            lobbyStatus.text = "PRESS <color=#2EC8FF>ENTER</color> OR <color=#FF3EA5>START</color> TO JOIN";
        else if (need > 0)
            lobbyStatus.text = $"NEED {need} MORE PLAYER{(need > 1 ? "S" : "")} FOR PVP  ·  ENTER / START TO JOIN";
        else
            lobbyStatus.text = $"STARTING IN {Mathf.CeilToInt(director.LobbyCountdown)}  ·  MORE CAN JOIN  ·  ENTER / START TO GO NOW";
    }

    private void UpdateStatus(MatchDirector director)
    {
        int alive = 0;
        foreach (var player in director.Players)
            if (player != null && player.isActiveAndEnabled)
                alive++;
        int seconds = Mathf.FloorToInt(director.MatchTime);
        string clock = $"{seconds / 60}:{seconds % 60:00}";
        statusText.text = MatchSettings.Mode == GameMode.Coop
            ? $"WAVE {Mathf.Max(1, director.Wave)}/{director.TotalWaves}   TURRETS {director.TurretsLeft}   {clock}"
            : $"PLAYERS LEFT {alive}   {clock}";

        var lines = new System.Text.StringBuilder();
        var arena = FindAnyObjectByType<ArenaBuilder>();
        if (arena != null && arena.Gates.Count > 0)
        {
            var gate = arena.Gates[0];
            lines.Append(gate.IsOn ? "<color=#FF3EA5>LASER GATES ON</color>" : $"GATES IN {Mathf.CeilToInt(gate.TimeUntilSwitch)}s").Append('\n');
        }
        var crates = FindAnyObjectByType<CrateSpawner>();
        if (crates != null && crates.Running)
            lines.Append($"SUPPLY DROP IN {Mathf.CeilToInt(crates.TimeUntilDrop)}s");
        eventsText.text = lines.ToString();
    }

    private void SyncCards(IReadOnlyList<PlayerInput> players)
    {
        while (cards.Count < players.Count)
        {
            var player = players[cards.Count];
            int index = player.playerIndex;
            var card = new PlayerCard { Player = player, Health = player.GetComponent<LocalHealth>(), Dash = player.GetComponent<PlayerDash>(), PowerUps = player.GetComponent<PowerUpReceiver>() };
            card.MaxHealth = card.Health != null ? Mathf.Max(1f, card.Health.CurrentHealth) : 1f;
            card.Root = UIFactory.Panel($"P{index + 1}", cardRow, new Vector2(CardWidth, 130f), LabPalette.PanelWhite, 24, 5f);
            card.Body = card.Root.Find("Body").GetComponent<Image>();
            var chip = UIFactory.Image("Chip", card.Body.transform, LabPalette.Player(index), RoundedSprite.Get(20));
            UIFactory.Place(chip.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(96f, 96f));
            card.Name = UIFactory.Label("Name", chip.transform, $"P{index + 1}", 44f, Color.white);
            UIFactory.Stretch(card.Name.rectTransform);

            card.HealthFill = Bar(card.Body.transform, new Vector2(124f, 18f), new Vector2(226f, 30f), LabPalette.Player(index));
            card.DashFill = Bar(card.Body.transform, new Vector2(124f, -22f), new Vector2(226f, 14f), LabPalette.Ink);
            card.Note = UIFactory.Label("Note", card.Body.transform, "", 22f, LabPalette.Ink, TextAlignmentOptions.Left, 0f);
            UIFactory.Place(card.Note.rectTransform, new Vector2(0f, 0.5f), new Vector2(124f, -46f), new Vector2(240f, 30f));
            UIFactory.Motion(card.Root, UIMotion.Kind.PopIn);
            cards.Add(card);
        }
    }

    private void PositionHealthCards()
    {
        if (cards.Count == 0)
            return;

        float screenWidth = ((RectTransform)cardRow.parent).rect.width;
        float groupWidth = cards.Count * CardWidth + (cards.Count - 1) * CardSpacing;
        float rightEdge = screenWidth - AmmoHud.PlateWidth - 2f * AmmoHud.Margin;
        float scale = Mathf.Clamp01((rightEdge - AmmoHud.Margin) / groupWidth);
        float center = Mathf.Min(screenWidth / 2f, rightEdge - groupWidth * scale / 2f);
        cardRow.localScale = new Vector3(scale, scale, 1f);
        cardRow.anchoredPosition = new Vector2(center - screenWidth / 2f, 24f);
    }

    private static RectTransform Bar(Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var back = UIFactory.Image("Bar", parent, LabPalette.PanelGrey, RoundedSprite.Get(8));
        UIFactory.Place(back.rectTransform, new Vector2(0f, 0.5f), position, size);
        back.rectTransform.pivot = new Vector2(0f, 0.5f);
        var fill = UIFactory.Image("Fill", back.transform, color, RoundedSprite.Get(8));
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        return fill.rectTransform;
    }

    private static void UpdateCard(PlayerCard card)
    {
        bool alive = card.Player != null && card.Player.isActiveAndEnabled;
        float health = alive && card.Health != null ? Mathf.Clamp01(card.Health.CurrentHealth / card.MaxHealth) : 0f;
        card.HealthFill.anchorMax = new Vector2(health, 1f);
        card.DashFill.anchorMax = new Vector2(alive && card.Dash != null ? card.Dash.CooldownFraction : 0f, 1f);
        card.Body.color = alive ? LabPalette.PanelWhite : LabPalette.PanelGrey;
        if (!alive)
            card.Note.text = "ELIMINATED";
        else if (card.PowerUps != null && card.PowerUps.Active.HasValue)
            card.Note.text = (card.PowerUps.Active == PowerUp.SpeedSurge ? "SPEED SURGE " : "INFINITE DASH ") + Mathf.CeilToInt(card.PowerUps.Remaining) + "s";
        else
            card.Note.text = card.Dash != null && card.Dash.CooldownFraction >= 1f ? "DASH READY" : "";
    }
}

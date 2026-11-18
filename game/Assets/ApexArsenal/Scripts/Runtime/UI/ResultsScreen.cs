using TMPro;
using UnityEngine;

// End-of-match card: VICTORY / DEFEAT / "P2 WINS!" with Rematch and Main Menu.
public class ResultsScreen : MonoBehaviour
{
    public static void Show(MatchOutcome outcome, int winnerIndex, float matchTime)
    {
        UIFactory.EnsureEventSystem();
        var canvas = UIFactory.CreateCanvas("Results", 40);
        var root = canvas.transform;
        canvas.gameObject.AddComponent<ResultsScreen>();
        UIFactory.Dim(root, 0.45f);

        string title;
        string subtitle;
        Color color;
        switch (outcome)
        {
            case MatchOutcome.Victory:
                title = "TEST PASSED!";
                subtitle = "Every sentry neutralised. The lab is impressed.";
                color = LabPalette.Lime;
                break;
            case MatchOutcome.Defeat:
                title = "TEST FAILED";
                subtitle = "All test subjects eliminated. Please try again.";
                color = LabPalette.Danger;
                break;
            case MatchOutcome.Winner:
                title = $"P{winnerIndex + 1} WINS!";
                subtitle = "Last test subject standing.";
                color = LabPalette.Player(winnerIndex);
                break;
            default:
                title = "DRAW!";
                subtitle = "Nobody survived. Impressive, in a way.";
                color = LabPalette.Violet;
                break;
        }

        var card = UIFactory.Panel("Card", root, new Vector2(1000f, 620f), LabPalette.PanelWhite);
        UIFactory.Place(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 620f));
        UIFactory.Motion(card, UIMotion.Kind.PopIn);

        var banner = UIFactory.Image("Banner", card, color, RoundedSprite.Get(24));
        UIFactory.Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 50f), new Vector2(820f, 150f));
        UIFactory.Motion(banner, UIMotion.Kind.Bob, 0f, 0.6f);
        var titleLabel = UIFactory.Label("Title", banner.transform, title, 96f, Color.white);
        UIFactory.Stretch(titleLabel.rectTransform);

        int seconds = Mathf.FloorToInt(matchTime);
        var info = UIFactory.Label("Info", card, $"{subtitle}\n<size=30>TIME {seconds / 60}:{seconds % 60:00}   ·   {ArenaPresets.Get(MatchSettings.Preset).Title}</size>", 38f, LabPalette.Ink, TextAlignmentOptions.Center, 0f);
        UIFactory.Place(info.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(900f, 140f));
        info.fontStyle = FontStyles.Normal;

        var column = UIFactory.Rect("Buttons", card);
        UIFactory.Place(column, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(520f, 220f));
        UIFactory.Column(column, 20f);
        var rematch = UIFactory.Button("Rematch", column, "REMATCH", LabPalette.NeonBlue, SceneFlow.Restart, new Vector2(500f, 96f), 42f);
        UIFactory.Button("Menu", column, "MAIN MENU", LabPalette.NeonPink, SceneFlow.LoadMainMenu, new Vector2(500f, 96f), 42f);
        UIFactory.Select(rematch);
    }
}

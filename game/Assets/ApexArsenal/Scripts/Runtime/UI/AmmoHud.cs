using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Shared by the match arenas and local co-op scenes. Slot zero sits closest to the corner.
[DefaultExecutionOrder(60)]
public class AmmoHud : MonoBehaviour
{
    public const float PlateWidth = 272f;
    public const float Margin = 24f;
    private const float PlateHeight = 96f;
    private readonly List<Plate> plates = new();
    private Canvas canvas;

    private class Plate
    {
        public RectTransform Root;
        public Image Chip;
        public TextMeshProUGUI Player;
        public Image Icon;
        public TextMeshProUGUI Weapon;
        public TextMeshProUGUI Count;
        public Image Fill;
        public (int Player, LocalPlayerShooting.WeaponSlot Weapon, int Rounds, int Capacity, bool Reloading, Sprite Icon)? Displayed;
    }

    private void Awake()
    {
        canvas = UIFactory.CreateCanvas("Ammo HUD", 11);
        canvas.GetComponent<GraphicRaycaster>().enabled = false;
    }

    private void OnEnable() => canvas.enabled = true;
    private void OnDisable()
    {
        if (canvas != null)
            canvas.enabled = false;
    }

    private void OnDestroy()
    {
        if (canvas != null)
            Destroy(canvas.gameObject);
    }

    private void LateUpdate()
    {
        int visible = 0;
        var director = MatchDirector.Instance;
        if (director == null || director.Current == MatchDirector.Phase.Playing || director.Current == MatchDirector.Phase.Countdown)
        {
            foreach (var player in PlayerInput.all)
            {
                if (!player.TryGetComponent<LocalPlayerShooting>(out var shooting) || !shooting.isActiveAndEnabled)
                    continue;
                if (visible == plates.Count)
                    plates.Add(CreatePlate(visible));
                var plate = plates[visible++];
                if (!plate.Root.gameObject.activeSelf)
                    plate.Root.gameObject.SetActive(true);
                Refresh(plate, player.playerIndex, shooting);
            }
        }
        for (int i = visible; i < plates.Count; i++)
            plates[i].Root.gameObject.SetActive(false);
    }

    private Plate CreatePlate(int slot)
    {
        var size = new Vector2(PlateWidth, PlateHeight);
        var root = UIFactory.Panel("Ammo plate", canvas.transform, size, LabPalette.FloorLine, 18, 5f);
        UIFactory.Place(root, Vector2.right, new Vector2(-Margin, Margin + slot * (PlateHeight + 10f)), size);
        var body = root.Find("Body");

        var chip = UIFactory.Image("Player chip", body, Color.white, RoundedSprite.Get(12));
        UIFactory.Place(chip.rectTransform, new Vector2(0f, 0.5f), new Vector2(5f, 0f), new Vector2(86f, 76f));
        var player = UIFactory.Label("Player", chip.transform, "", 20f, LabPalette.Ink, TextAlignmentOptions.Center, 0f);
        UIFactory.Place(player.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(80f, 25f));
        var icon = UIFactory.Image("Weapon silhouette", chip.transform, Color.white);
        icon.preserveAspect = true;
        UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(76f, 40f));

        var header = UIFactory.Label("Weapon", body, "", 17f, Color.white, TextAlignmentOptions.Right, 0f);
        UIFactory.Place(header.rectTransform, Vector2.one, new Vector2(-12f, -4f), new Vector2(145f, 21f));
        var count = UIFactory.Label("Rounds", body, "", 42f, LabPalette.PanelWhite, TextAlignmentOptions.Right, 0f);
        UIFactory.Place(count.rectTransform, Vector2.right, new Vector2(-12f, 13f), new Vector2(145f, 47f));
        var track = UIFactory.Image("Magazine", body, LabPalette.Ink);
        UIFactory.Place(track.rectTransform, Vector2.right, new Vector2(-12f, 6f), new Vector2(145f, 5f));
        var fill = UIFactory.Image("Rounds remaining", track.transform, Color.white);
        UIFactory.Stretch(fill.rectTransform);
        return new Plate { Root = root, Chip = chip, Player = player, Icon = icon, Weapon = header, Count = count, Fill = fill };
    }

    private static void Refresh(Plate plate, int index, LocalPlayerShooting shooting)
    {
        var state = (index, shooting.CurrentWeapon, shooting.RoundsLoaded, shooting.MagazineSize, shooting.IsReloading, shooting.WeaponIcon);
        if (plate.Displayed == state)
            return;
        plate.Displayed = state;

        Color accent = LabPalette.Player(index);
        plate.Chip.color = accent;
        plate.Player.SetText("P{0}", index + 1);
        plate.Icon.sprite = shooting.WeaponIcon;
        plate.Icon.enabled = plate.Icon.sprite != null;
        plate.Weapon.text = shooting.IsReloading ? "RELOADING" : shooting.CurrentWeapon switch
        {
            LocalPlayerShooting.WeaponSlot.Shotgun => "SHOTGUN",
            LocalPlayerShooting.WeaponSlot.Rifle => "RIFLE",
            _ => "PISTOL"
        };
        plate.Weapon.color = accent;
        plate.Count.SetText("{0} <size=55%>/ {1}</size>", (float)shooting.RoundsLoaded, (float)shooting.MagazineSize);
        plate.Fill.color = accent;
        plate.Fill.rectTransform.anchorMax = new Vector2((float)shooting.RoundsLoaded / shooting.MagazineSize, 1f);
    }
}

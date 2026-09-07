using UnityEngine;
using UnityEngine.UI;

// The upgrade shop. Built from code onto the HUD canvas and opened from the pause and
// game-over screens. It is a plain class rather than a MonoBehaviour because GameHUD
// already owns the canvas and the update loop - this just owns its own rows.
public class ShopPanel
{
    private const int RowCount = 5;
    private const float RowHeight = 78f;

    private GameObject root;
    private Text currencyText;
    private readonly Text[] levelTexts = new Text[RowCount];
    private readonly Text[] costTexts = new Text[RowCount];
    private readonly Button[] buyButtons = new Button[RowCount];
    private readonly Image[] buyImages = new Image[RowCount];

    public bool IsOpen => root != null && root.activeSelf;

    public void Build(Transform parent, Font font)
    {
        Image background = UIFactory.MakePanel(parent, "Shop Panel", new Color(0.03f, 0.04f, 0.07f, 0.93f));
        root = background.gameObject;
        UIFactory.Stretch(background.rectTransform);

        Text title = UIFactory.MakeText(root.transform, font, "Title", "UPGRADES", 58, TextAnchor.MiddleCenter);
        title.color = UIFactory.Accent;
        UIFactory.Place(title.rectTransform, new Vector2(0f, 300f), new Vector2(800f, 80f));
        UIFactory.AddOutline(title.gameObject);

        currencyText = UIFactory.MakeText(root.transform, font, "Shards", "", 34, TextAnchor.MiddleCenter);
        UIFactory.Place(currencyText.rectTransform, new Vector2(0f, 240f), new Vector2(800f, 50f));

        float top = 160f;
        for (int i = 0; i < RowCount; i++)
        {
            BuildRow(font, i, top - i * RowHeight);
        }

        UIFactory.MakeButton(root.transform, font, "Back", new Vector2(260f, 60f),
                             new Vector2(0f, -280f), () => SetOpen(false), out _);

        root.SetActive(false);
    }

    private void BuildRow(Font font, int index, float y)
    {
        UpgradeType type = PlayerProgress.All[index];
        PlayerProgress.UpgradeDef def = PlayerProgress.Definition(type);

        Text name = UIFactory.MakeText(root.transform, font, def.Name, def.Name, 30, TextAnchor.MiddleLeft);
        UIFactory.Place(name.rectTransform, new Vector2(-250f, y + 13f), new Vector2(340f, 36f));

        Text description = UIFactory.MakeText(root.transform, font, def.Name + " Desc", def.Description,
                                              20, TextAnchor.MiddleLeft);
        description.color = new Color(1f, 1f, 1f, 0.5f);
        UIFactory.Place(description.rectTransform, new Vector2(-250f, y - 13f), new Vector2(340f, 30f));

        levelTexts[index] = UIFactory.MakeText(root.transform, font, def.Name + " Level", "", 26,
                                               TextAnchor.MiddleCenter);
        UIFactory.Place(levelTexts[index].rectTransform, new Vector2(60f, y), new Vector2(160f, 40f));

        int captured = index;
        buyButtons[index] = UIFactory.MakeButton(root.transform, font, "Buy", new Vector2(230f, 56f),
                                                 new Vector2(280f, y), () => Purchase(captured),
                                                 out costTexts[index]);
        buyImages[index] = buyButtons[index].targetGraphic as Image;
        costTexts[index].fontSize = 24;
    }

    private void Purchase(int index)
    {
        if (PlayerProgress.Buy(PlayerProgress.All[index])) Refresh();
    }

    public void SetOpen(bool open)
    {
        if (root == null) return;
        root.SetActive(open);
        if (open) Refresh();
    }

    public void Refresh()
    {
        if (root == null) return;

        currencyText.text = PlayerProgress.Currency + "  SHARDS";

        for (int i = 0; i < RowCount; i++)
        {
            UpgradeType type = PlayerProgress.All[i];
            PlayerProgress.UpgradeDef def = PlayerProgress.Definition(type);
            int level = PlayerProgress.GetLevel(type);

            levelTexts[i].text = "LV " + level + " / " + def.MaxLevel;
            levelTexts[i].color = level > 0 ? UIFactory.Accent : new Color(1f, 1f, 1f, 0.55f);

            bool maxed = PlayerProgress.IsMaxed(type);
            bool affordable = PlayerProgress.CanBuy(type);

            costTexts[i].text = maxed ? "MAXED" : PlayerProgress.GetCost(type) + " SHARDS";
            costTexts[i].color = maxed
                ? new Color(1f, 1f, 1f, 0.4f)
                : affordable ? Color.white : new Color(1f, 0.55f, 0.5f, 0.75f);

            buyButtons[i].interactable = affordable;
            if (buyImages[i] != null) buyImages[i].color = Color.white;
        }
    }
}

# 2616 Cafe – Menu Card (AR image target)

This card is both the printed menu and the AR marker the app tracks.

| File | Use |
|---|---|
| `2616_Cafe_Menu_Card_A5.pdf` | Print this (A5, 148 × 210 mm) |
| `2616_Cafe_Menu_Card_A5_300dpi.png` | Same design as an image (1748 × 2480 px, 300 dpi) |
| `source/menu_card.html` | Editable source (open in Chrome; fonts in `source/fonts`) |
| `../Assets/ARMenu/Marker/menu_card_2616.png` | The tracking texture Vuforia uses (1024 px wide) |

## Printing (important for AR)
- Print at **100 % / actual size** on **A5** – do not use "fit to page". The app expects the card to be **148 mm wide**.
- Use **matte** paper or card (glossy paper reflects light and hurts tracking).
- Keep it flat and well lit.
- Printing on a different size? Change `MarkerPrintedWidth` in `Assets/ARMenu/Editor/ARMenuEditorTools.cs`
  (metres) and run **Tools → AR Menu → Use 2616 Menu Card As Image Target**.

## Changing the design
Edit `source/menu_card.html`, re-render it (e.g. Chrome headless screenshot at 1748 × 2480), replace
`Assets/ARMenu/Marker/menu_card_2616.png` with a 1024-px-wide copy, then run the menu item above.
If you change prices or dishes on the card, update `Assets/ARMenu/Resources/ARMenu/menu_data.json` too.

## Credits
Done by: Smriti Nukula & Shreyas Abiram YR · Fonts: Fredoka and Pacifico (SIL Open Font License 1.1, licences in `source/fonts`).

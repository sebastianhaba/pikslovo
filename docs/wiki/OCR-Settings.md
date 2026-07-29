# OCR settings guide

Pikslovo captures the selected part of the screen, sends an image to Google
Cloud Vision for text recognition, translates the recognised text, and draws
the result as an overlay. The settings in **Recognition** control that
pipeline. They are stored locally and apply to the next translation you run.

Start with the default values. Change one setting at a time and compare the
result on the same game screen: the best values depend on the size, contrast,
and style of the game's text.

## Recognition confidence

**Default:** `0.6`
**Range:** `0.0` to `1.0`, in `0.1` steps

This is the minimum confidence reported by Google Cloud Vision for an OCR
paragraph. Paragraphs below the selected value are discarded before they are
sent to Google Cloud Translation or shown in the overlay.

- Raise it when the overlay includes decorative UI elements, texture noise, or
  clearly incorrect text. This produces fewer, more reliable translations.
- Lower it when small, stylised, low-contrast, or partially obscured dialogue
  is missing. This can also allow more OCR mistakes through.

This setting affects which recognised paragraphs are translated. It does not
make Cloud Vision itself more or less accurate.

## OCR image scale

**Default:** `1.0x`
**Available values:** `0.25x`, `0.5x`, `0.75x`, and `1.0x`

This scales the captured image before it is encoded and uploaded to Cloud
Vision. The original full-resolution screenshot is still used as the overlay
background; Pikslovo converts the recognised text positions back to that
original size.

- Use `1.0x` for the best chance of reading small text. It usually sends the
  largest image and can take longer to encode and upload.
- Reduce the scale to make requests smaller and often faster, especially for
  high-resolution screens or large capture areas.
- Do not reduce it too far if dialogue, subtitles, or menu labels are already
  small: downscaling removes detail and may make them unreadable.

Image scale changes the amount of image detail Cloud Vision receives. It is
usually the first setting to reduce when transfers are slow, and the first one
to restore to `1.0x` when OCR misses small text.

## Compress OCR image to JPEG

**Default:** on

When enabled, Pikslovo uploads the OCR image as JPEG. When disabled, it sends
a PNG instead.

- **JPEG on** normally creates a much smaller upload, which can reduce network
  time and data use. JPEG is lossy, so it may add artefacts around very small,
  sharp, or high-contrast characters.
- **JPEG off (PNG)** preserves the captured pixels without JPEG artefacts. It
  can help with pixel fonts, fine outlines, and small text, but files are
  commonly larger and can make the request slower.

This affects only the image sent for OCR. It does not lower the quality of the
screenshot displayed behind the translation overlay.

## JPEG quality

**Default:** `85%`
**Range:** `50%` to `100%`, in `5%` steps
**Available when:** **Compress OCR image to JPEG** is on

JPEG quality controls the trade-off within JPEG compression.

- Lower values produce smaller uploads and may improve responsiveness on slow
  connections, but introduce stronger compression artefacts that can hurt OCR.
- Higher values retain more edge detail and are preferable for small or
  stylised text, at the cost of larger uploads.
- `85%` is a practical default. Try `95%` or `100%` before switching to PNG
  when JPEG OCR is close but unreliable.

At `100%`, the image is still JPEG and therefore not identical to PNG; select
PNG if avoiding JPEG compression artefacts is important.

## Text grouping strength

**Default:** `0.25`
**Range:** `0.25` to `1.0`, in `0.05` steps

Cloud Vision returns text as separate paragraphs. Pikslovo then groups nearby
paragraphs into larger dialogue regions before translating and drawing them.
This setting controls how readily vertically adjacent, aligned paragraphs are
combined.

- A lower value keeps regions separate more easily. Use it if unrelated UI
  labels or separate dialogue bubbles are being joined into one translation
  box.
- A higher value joins nearby lines more readily. Use it when one dialogue
  block is split into several translated boxes or loses its intended reading
  order.

Grouping does not change what Vision recognises. It changes the text chunks
sent to translation and the number, size, and placement of overlay boxes.

## Font scale

**Default:** `1.3x`
**Range:** `1.0x` to `3.0x`, in `0.1x` steps

This multiplies the size of translated text drawn inside each black overlay
box.

- Increase it when translations are difficult to read from your normal playing
  distance.
- Decrease it when translated text is crowded, wraps too often, or is clipped
  within a small original-text region.

Font scale does not affect OCR, network traffic, or translation. It only
affects the readability and fit of the final overlay.

## Hide identical translations

**Default:** on

When enabled, Pikslovo does not draw a translation box if the recognised source
text and translated text are the same after trimming leading/trailing spaces
and ignoring letter case. For example, an unchanged name, number, or already
target-language label will not be covered by a black box.

- Keep it on for a cleaner overlay when the source and target languages share
  words, names, numbers, or UI labels.
- Turn it off when you want to see every recognised region, including text
  whose translation happens to be unchanged. This can be useful while testing
  OCR coverage.

The comparison is made after translation. Turning the option on does not stop
the text from being sent to Google Cloud Translation; it only controls whether
the final region is drawn.

## Suggested troubleshooting order

If text is not recognised, first use `1.0x` image scale. Then try a higher
JPEG quality or PNG, and lower **Recognition confidence** if Vision is
recognising the text with low confidence. If the text is recognised but the
overlay boxes are awkward, adjust **Text grouping strength** and **Font scale**
instead.

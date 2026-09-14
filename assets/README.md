# Icon provenance

Created with the built-in image_gen tool for this project.

Prompt: A polished Windows desktop icon for ImageZipMerger: two overlapping photo cards with a mountain and sun, integrated with a clear vertical zipper on the right. Rounded square deep blue tile, white photo shapes, cyan accent, small warm amber zipper pull. Crisp compact geometry, centered padding, legible at small sizes. No text or watermark.

assets/icon.png is the resized 256px project source. build-icon.ps1 packages PNG frames at 16, 24, 32, 48, 64, 128 and 256 pixels into assets/ImageZipMerger.ico. The application embeds the ICO; no runtime icon dependency is needed.

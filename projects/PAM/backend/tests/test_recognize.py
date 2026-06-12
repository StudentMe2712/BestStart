"""Unit tests — attachment recognition dispatch (image→vision, doc→markitdown)."""
import app.content as content


async def test_image_uses_vision(monkeypatch):
    async def fake_describe(data, mime):
        return "описание изображения"

    # recognize_attachment делает `from .llm import describe_image` внутри —
    # патчим атрибут модуля, он резолвится при вызове.
    monkeypatch.setattr("app.llm.describe_image", fake_describe)
    kind, text = await content.recognize_attachment("photo.PNG", b"\x89PNG")
    assert kind == "image"
    assert text == "описание изображения"


async def test_document_uses_markitdown(monkeypatch):
    monkeypatch.setattr(
        content, "extract_file_text", lambda name, data: (None, "извлечённый текст")
    )
    kind, text = await content.recognize_attachment("report.txt", b"hello")
    assert kind == "document"
    assert text == "извлечённый текст"

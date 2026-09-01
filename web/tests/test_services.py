from app.services import normalize_phrases


def test_normalize_phrases_removes_blanks_and_keeps_source_order() -> None:
    assert normalize_phrases([" first ", "", "first", "second", "  "]) == ["first", "second"]

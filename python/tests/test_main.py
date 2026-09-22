import pytest
from main import greet


def test_greet_returns_greeting():
    assert greet("Shreya") == "Hello, Shreya!"


def test_greet_rejects_empty_string():
    with pytest.raises(ValueError):
        greet("")


def test_greet_rejects_non_string():
    with pytest.raises(ValueError):
        greet(123)

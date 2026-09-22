"""Sample entry point for the assessment solution."""


def greet(name: str) -> str:
    """Return a greeting for the given name.

    Raises:
        ValueError: if name is empty or not a string.
    """
    if not isinstance(name, str) or not name.strip():
        raise ValueError("name must be a non-empty string")
    return f"Hello, {name}!"


if __name__ == "__main__":
    print(greet("world"))

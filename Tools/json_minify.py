from json import dump, load
from json.decoder import JSONDecodeError
from pathlib import Path
from glob import iglob


def main():
    resources: Path = Path("../Resources")
    a = resources.resolve()
    errors = []

    for fn in a.rglob("**/meta.json"):
        try:
            # Read and write are separate so we can fix file formats
            with fn.open("r", encoding="utf-8-sig") as f:
                loaded: dict = load(f)
                states = loaded.get("states")

                for state in states:
                    state: dict

                    if state.get("directions") == 1:
                        state.pop("directions")
                        print(f"Pruned unnecessary direction from {fn}")

                    # Prune unnecessary delays
                    delays = state.get("delays", [])

                    if delays:
                        delay_prune = True
                        for delay in state.get("delays", []):
                            delay: list[int]

                            if len(delay) != 1 or delay[0] != 1.0:
                                delay_prune = False
                                break

                        if delay_prune:
                            state.pop("delays")
                            print(f"Pruned delays from {fn}")

            with fn.open("w", encoding="utf-8") as f:
                dump(loaded, f, separators=(",", ":"))
        except JSONDecodeError:
            errors.append(fn)

        print(f"Minified {fn}")

    for error in errors:
        print(f"JSON decode error minifying {error}")


if __name__ == "__main__":
    main()

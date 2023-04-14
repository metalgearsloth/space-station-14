from json import dump, dumps, load
from json.decoder import JSONDecodeError
from pathlib import Path
from collections import OrderedDict


def main():
    resources: Path = Path("../Resources")
    a = resources.resolve()
    errors = []
    sort_order = [
        "version",
        "license",
        "copyright",
        "size",
        "states"
    ]

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

            with fn.open("w", encoding="utf-8-sig") as f:
                sorted_dict = OrderedDict(sorted(loaded.items(), key=lambda x: sort_order.index(x[0]) if x[0] in sort_order else 1000))
                dump(sorted_dict, f, ensure_ascii=False, sort_keys=False, indent=2)
        except JSONDecodeError:
            errors.append(fn)

        print(f"Minified {fn}")

    for error in errors:
        print(f"JSON decode error minifying {error}")


if __name__ == "__main__":
    main()

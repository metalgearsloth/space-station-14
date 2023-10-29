# Converts all audio resources to prototypes that can be used by the engine.
import os

AUDIO_EXTENSIONS = [
    ".ogg",
    ".wav",
]

AUDIO_PROTOTYPE_PATH = "./audio.yml"


def valid_extension(file: str) -> bool:
    file_extension = os.path.splitext(file)

    for ext in AUDIO_EXTENSIONS:
        if file_extension == ext:
            return True
        
    return False


def get_audio_prototype(folder: str):
    return os.path.join(folder, AUDIO_PROTOTYPE_PATH)


def copy_audio_prototypes(folder: str):
    data = []

    with open(get_audio_prototype(folder), "w") as f:
        for audio_file in os.listdir(folder):
            if os.path.isdir(audio_file) or not valid_extension(audio_file):
                continue

            

    

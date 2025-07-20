SET X to 0
SET Y to 0
RANDOM A
RANDOM B
RANDOM C

JUMP RenderLoop

DrawLoop:
    DRAW 5 5 255 0 0
    REFRESHSCREEN
    RANDOM A
    RANDOM B
    RANDOM C
    X = X + 1
    JUMP IncY if zero
    Y = Y + 0
    JUMP RenderLoop if zero
    JUMP DrawLoop

IncY:
    Y = Y + 1
    JUMP L16

RenderLoop:
    REFRESHSCREEN
    JUMP DrawLoop

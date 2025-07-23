SET X to 0
SET Y to 0
RANDOM A
RANDOM B
RANDOM C

JUMP RenderLoop

DrawLoop:
    DRAW X X A B C
    RANDOM A
    RANDOM B
    RANDOM C
    X = X + 1
    JUMP RenderLoop if carry
    JUMP DrawLoop

RenderLoop:
    REFRESHSCREEN
    JUMP DrawLoop
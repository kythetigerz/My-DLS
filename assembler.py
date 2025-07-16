# Let's rewrite the assembler to match the exact spec given

import re
import sys
import pyperclip

# Register to binary mapping (3-bit values)
REGISTERS = {
    'A': 0b000,
    'B': 0b001,
    'C': 0b010,
    'D': 0b011,
    'X': 0b100,
    'Y': 0b101,
}

# Opcode mapping (8-bit)
OPCODES = {
    'NOP': 0b00000000,
    'ALU': 0b00000001,
    'STORE': 0b00000010,
    'LOAD': 0b00000011,
    'SET': 0b00000100,
    'JUMP': 0b00000101,
    'DRAW': 0b00000110,
    'CLEARSCREEN': 0b00000111,
    'REFRESHSCREEN': 0b00001000,
    'RANDOM': 0b00001001,
    'INCDEC': 0b00001010,
    'HALT': 0b11111111
}

ALU_OPS = {
    '+': 0,
    '*': 1,
    'NOR': 2,
    'OR': 3,
    'XNOR': 4,
    'AND': 5,
    'NOT': 6,
    'XOR': 7,
}

JUMP_TYPES = {
    None: 0,
    'ZERO': 1,
    'CARRY': 2,
    'KEY': 3
}

def parse_number(token):
    token = token.upper()
    if token.startswith('0X'):
        return int(token, 16)
    if token.startswith('0B'):
        return int(token, 2)
    return int(token)

def first_pass(lines):
    labels = {}
    pc = 0
    for line in lines:
        if ':' in line:
            label = line.split(':')[0].strip()
            labels[label.upper()] = pc
        elif line.strip() and not line.strip().startswith('//'):
            pc += 1
    return labels

def parse_instruction(line, labels):
    line = line.strip().upper()
    line = re.sub(r'//.*', '', line)
    if not line or line.endswith(':'):
        return None

    # HALT
    if line == 'HALT':
        return (0, 0, 0, OPCODES['HALT'])

    # NOP
    if line == 'NOP':
        return (0, 0, 0, OPCODES['NOP'])

    # CLEARSCREEN / REFRESHSCREEN
    if line == 'CLEARSCREEN':
        return (0, 0, 0, OPCODES['CLEARSCREEN'])
    if line == 'REFRESHSCREEN':
        return (0, 0, 0, OPCODES['REFRESHSCREEN'])

    # RANDOM R
    if line.startswith('RANDOM'):
        _, reg = line.split()
        return (0, 0, REGISTERS[reg], OPCODES['RANDOM'])

    # INC/DEC R N
    if line.startswith('INC') or line.startswith('DEC'):
        parts = line.split()
        inc = 1 if parts[0] == 'INC' else 0
        reg = REGISTERS[parts[1]]
        amount = parse_number(parts[2]) & 0x7F
        data1 = (inc << 7) | amount
        return (0, reg, data1, OPCODES['INCDEC'])

    # SET R TO N
    if line.startswith('SET'):
        parts = line.split()
        reg = REGISTERS[parts[1]]
        value = parse_number(parts[-1])
        return (0, reg, value, OPCODES['SET'])

    # STORE R INTO ADDR
    if line.startswith('STORE') and 'INTO' in line:
        parts = line.split()
        reg = REGISTERS[parts[1]]
        addr = parse_number(parts[3])
        return (0, reg, addr, OPCODES['STORE'])

    # LOAD ADDR INTO R
    if line.startswith('LOAD') or (line.startswith('STORE') and 'INTO' not in line):
        parts = line.split()
        addr = parse_number(parts[1])
        reg = REGISTERS[parts[3]]
        return (0, reg, addr, OPCODES['LOAD'])

    # DRAW X Y R G B
    if line.startswith('DRAW'):
        parts = line.split()
        x = parse_number(parts[1])
        y = parse_number(parts[2])
        r = parse_number(parts[3]) & 0xF
        g = parse_number(parts[4]) & 0xF
        b = parse_number(parts[5]) & 0xF
        data3 = b
        data2 = (r << 4) | g
        data1 = x
        opcode = OPCODES['DRAW']
        return (data3, data2, data1, opcode)

    # JUMP label IF condition
    if line.startswith('JUMP'):
        parts = line.split()
        label = parts[1]
        jump_type = parts[3] if len(parts) > 3 else None
        
        # Check if the label exists in our labels dictionary
        if label in labels:
            addr = labels[label]
        else:
            # Try to parse it as a number, but if it fails, raise a more helpful error
            try:
                addr = parse_number(label)
            except ValueError:
                raise ValueError(f"Unknown jump label: '{label}'. Make sure the label is defined with a colon (e.g., '{label}:').")
        
        jt = JUMP_TYPES.get(jump_type, 0)
        return (0, jt, addr, OPCODES['JUMP'])

    # ALU binary op: A + B = C
    match = re.match(r'([A-Z]+)\s*([+*/]|NAND|AND|OR|NOR|XOR|XNOR)\s*([A-Z]+)\s*=\s*([A-Z]+)', line)
    if match:
        reg1, op, reg2, outreg = match.groups()
        opnum = ALU_OPS[op]
        r1 = REGISTERS[reg1]
        r2 = REGISTERS[reg2]
        out = REGISTERS[outreg]
        data3 = opnum                      # operation
        data1 = (r2 << 4) | r1            # reg2 (high nibble), reg1 (low)
        data2 = out                       # output reg
        return (data3, data2, data1, OPCODES['ALU'])

    # ALU unary op: NOT A = B
    match = re.match(r'(NOT)\s+([A-Z]+)\s*=\s*([A-Z]+)', line)
    if match:
        op, reg1, outreg = match.groups()
        opnum = ALU_OPS[op]
        r1 = REGISTERS[reg1]
        r2 = 0                             # unused input
        out = REGISTERS[outreg]
        data3 = opnum
        data1 = (r2 << 4) | r1
        data2 = out
        return (data3, data2, data1, OPCODES['ALU'])

    raise ValueError(f"Unknown instruction: {line}")

def assemble(filepath):
    with open(filepath, 'r') as f:
        lines = f.readlines()

    labels = first_pass(lines)

    instrs = []
    instrs.append((0, 0, 0, OPCODES['NOP']))
    instrs.append((0, 0, 0, OPCODES['NOP']))
    for line in lines:
        result = parse_instruction(line, labels)
        if result:
            instrs.append(result)

    if not instrs or instrs[-1][3] != OPCODES['HALT']:
        instrs.append((0, 0, 0, OPCODES['HALT']))

    while len(instrs) < 256:
        instrs.append((0, 0, 0, OPCODES['NOP']))

    rom1 = [f"{data1:08b}{opcode:08b}" for data3, data2, data1, opcode in instrs]
    rom2 = [f"{data3:08b}{data2:08b}" for data3, data2, data1, opcode in instrs]

    pyperclip.copy("\n".join(rom1))
    input("ROM1 copied to clipboard. Press Enter to copy ROM2...")
    pyperclip.copy("\n".join(rom2))
    print("ROM2 copied to clipboard.")


if __name__ == '__main__':
    assemble("code.asm")

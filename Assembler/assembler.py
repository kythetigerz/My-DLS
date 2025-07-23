# Assembler for DLS CPU

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

# Value Types as per hardware specification
VALUE_TYPES = {
    'REGISTER': 0b00,
    'BUILTIN': 0b01,
    'RAM': 0b10
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
    'STACK': 0b00001010,
    'HALT': 0b11111111
}

# Stack pointer for function calls (starts at 0xFF and decrements)
STACK_START = 0xFF

# Stack Types as per hardware specification
STACK_TYPES = {
    'RETURN': 0b00,
    'PARAMETERS': 0b01,
    'GENERAL': 0b10
}

# Stack Operations
STACK_OPS = {
    'PUSH': 0b00,
    'POP': 0b01
}

# ALU operations according to documentation (0-indexed from top to bottom)
ALU_OPS = {
    '+': 0,        # addition
    '-': 1,        # subtraction
    '*': 2,        # multiplication
    '/': 3,        # division
    'NAND': 4,     # nand
    'AND': 5,      # and
    'NOT': 6,      # not
    'OR': 7,       # or
    'NOR': 8,      # nor
    'XOR': 9,      # xor
    'XNOR': 10,    # xnor
    'COMPARE': 11, # compare
    'COMPARE_SIGNED': 12, # compare signed
}

# Keys mapping
KEYS = {
    'W': 0,
    'A': 1,
    'S': 2,
    'D': 3,
    'ANY': 0xF
}

JUMP_TYPES = {
    None: 0b00,       # unconditional jump
    'ZERO': 0b01,     # jump on zero
    'CARRY': 0b10,    # jump on carry
    'KEY': 0b11,      # jump on key
}

def parse_number(token):
    token = token.upper()
    if token.startswith('0X'):
        return int(token, 16)
    if token.startswith('0B'):
        return int(token, 2)
    try:
        return int(token)
    except ValueError:
        raise ValueError(f"invalid literal for int() with base 10: '{token}'")

def first_pass(lines):
    """First pass to identify functions and fake functions only"""
    functions = {}
    fake_functions = {}
    functions_with_return = set()  # Track which functions have RETURN statements
    
    # First, identify functions and fake functions
    for i, line in enumerate(lines):
        clean_line = re.sub(r'//.*', '', line).strip()
        if not clean_line:
            continue
            
        if ':' in clean_line:
            label_part = clean_line.split(':')[0].strip()
            
            # Check for function syntax [function_name]: or [function_name]: fake
            if label_part.startswith('[') and label_part.endswith(']'):
                func_name = label_part[1:-1].upper()
                # Check if it's a fake function
                rest_of_line = clean_line.split(':', 1)[1].strip().upper()
                if rest_of_line == 'FAKE':
                    fake_functions[func_name] = {'start_line': i, 'instructions': []}
                else:
                    # Functions will be placed after HALT, we'll calculate their addresses later
                    functions[func_name] = None  # Placeholder
            else:
                # Check if it's a fake function without brackets (label: fake)
                rest_of_line = clean_line.split(':', 1)[1].strip().upper()
                if rest_of_line == 'FAKE':
                    func_name = label_part.upper()
                    fake_functions[func_name] = {'start_line': i, 'instructions': []}
                else:
                    # Check if this looks like a function (contains "Loop" or common function patterns)
                    # or if it has instructions following it that suggest it's a function
                    func_name = label_part.upper()
                    
                    # Look ahead to see if this should be treated as a function
                    is_function = False
                    has_return = False
                    
                    # Look ahead to see if it has a return statement or ends with an unconditional jump
                    for j in range(i + 1, min(i + 20, len(lines))):  # Look ahead up to 20 lines
                        next_line = re.sub(r'//.*', '', lines[j]).strip()
                        if not next_line:
                            continue
                        if next_line.upper() == 'RETURN':
                            is_function = True
                            has_return = True
                            break
                        # Check if it ends with an unconditional jump (JUMP without IF condition)
                        if next_line.upper().startswith('JUMP ') and ' IF ' not in next_line.upper():
                            parts = next_line.upper().split()
                            # Make sure it's not a conditional jump (ZERO, CARRY, KEY)
                            if len(parts) == 2 or (len(parts) > 2 and parts[2] not in ['ZERO', 'CARRY', 'KEY']):
                                is_function = True
                                break
                        if ':' in next_line:  # Hit another label
                            break
                    
                    # Only treat as function if it has explicit function patterns AND has return/jump behavior
                    if not is_function:
                        # Be more restrictive - only treat as function if it has explicit naming patterns
                        if any(pattern in func_name for pattern in ['FUNC', 'SUB', 'PROC', 'ROUTINE']):
                            is_function = True
                    
                    if is_function:
                        # Treat as function
                        functions[func_name] = None  # Placeholder
                        if has_return:
                            functions_with_return.add(func_name)
    
    # Collect fake function instructions and check for RETURN statements in fake functions
    for func_name, func_info in fake_functions.items():
        start_line = func_info['start_line']
        instructions = []
        has_return = False
        
        # Find instructions until next function/label or end of file
        for i in range(start_line + 1, len(lines)):
            line = re.sub(r'//.*', '', lines[i]).strip()
            if not line:
                continue
            
            # Stop if we hit another function or label
            if ':' in line:
                break
                
            instructions.append(line)
            if line.upper() == 'RETURN':
                has_return = True
        
        func_info['instructions'] = instructions
        if has_return:
            functions_with_return.add(func_name)
    
    return functions, fake_functions, functions_with_return

def count_instruction_increment(line, fake_functions, functions_with_return=None):
    """Count how many machine instructions a single source line will generate"""
    if functions_with_return is None:
        functions_with_return = set()
    
    line = line.strip().upper()
    
    # RETURN generates 2 instructions (LOAD + JUMP)
    if line == 'RETURN':
        return 2
    
    # IF statements are fake and don't generate instructions directly
    if line.startswith('IF ') or line == 'ELSE' or line == 'END':
        return 0
    
    # Function calls - check if they need return address handling
    if line.startswith('JUMP') and '[' in line and ']' in line:
        func_name_match = re.search(r'\[([^\]]+)\]', line)
        if func_name_match:
            func_name = func_name_match.group(1).upper()
            if func_name in fake_functions:
                # Fake function - count its instructions recursively
                total_count = 0
                for instr in fake_functions[func_name]['instructions']:
                    total_count += count_instruction_increment(instr, fake_functions, functions_with_return)
                return total_count
            else:
                # Real function call - only generates 2 instructions if function has RETURN
                if func_name in functions_with_return:
                    return 2  # push return + jump
                else:
                    return 1  # just jump
    
    # Function calls without brackets to fake functions
    if line.startswith('JUMP'):
        parts = line.split()
        if len(parts) >= 2:
            target = parts[1].upper()
            if target in fake_functions:
                # Fake function - count its instructions recursively
                total_count = 0
                for instr in fake_functions[target]['instructions']:
                    total_count += count_instruction_increment(instr, fake_functions, functions_with_return)
                return total_count
            else:
                # Real function call - only generates 2 instructions if function has RETURN
                if target in functions_with_return:
                    return 2  # push return + jump
                else:
                    return 1  # just jump
    
    # Most other instructions generate 1 machine instruction
    return 1

def parse_value(value_str):
    """Parse a value which could be a register, a direct value, or a memory address."""
    value_str = value_str.strip().upper()
    
    # Check if it's a register
    if value_str in REGISTERS:
        return (VALUE_TYPES['REGISTER'], REGISTERS[value_str])
    
    # Check if it's a memory address (indicated by [])
    if value_str.startswith('[') and value_str.endswith(']'):
        addr = parse_number(value_str[1:-1])
        return (VALUE_TYPES['RAM'], addr & 0xFF)
    
    # Otherwise, it's a built-in value (constant)
    try:
        value = parse_number(value_str)
        return (VALUE_TYPES['BUILTIN'], value & 0xFF)
    except ValueError:
        raise ValueError(f"Invalid value: {value_str}")

def parse_instruction(line, labels, functions=None, line_labels=None, call_stack_ptr=None, fake_functions=None):
    line = line.strip().upper()
    line = re.sub(r'//.*', '', line)
    if not line or line.endswith(':'):
        return None

    # HALT
    if line == 'HALT':
        return (0, 0, 0, OPCODES['HALT'])
    
    # RETURN - Pop return address from stack and jump to it
    if line == 'RETURN':
        # Return a special marker that the assembler will handle
        # This will be expanded to: POP FROM RETURN TO X, JUMP X
        return ('RETURN', 0, 0, 'SPECIAL')

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
        parts = line.split()
        if len(parts) != 2:
            raise ValueError(f"RANDOM instruction requires exactly one register parameter, got {len(parts)-1}: {line}")
        reg = parts[1]
        if reg.upper() not in REGISTERS:
            raise ValueError(f"Invalid register in RANDOM instruction: {reg}")
        reg_code = REGISTERS[reg.upper()]
        # DATA3: null, DATA2: register, DATA1: null
        return (0, reg_code, 0, OPCODES['RANDOM'])

    # PUSH [stack name] [value] - Push value onto stack
    if line.startswith('PUSH'):
        parts = line.split()
        # Default to GENERAL stack if not specified
        stack_type = STACK_TYPES['GENERAL']
        value = 0
        reg = 0xF  # 0xF means no register (use data instead)
        
        if len(parts) >= 3:
            # PUSH [stack name] [value/register]
            stack_name = parts[1].upper()
            if stack_name in STACK_TYPES:
                stack_type = STACK_TYPES[stack_name]
                value_str = parts[2].upper()
            else:
                # First argument is not a stack name, treat as PUSH [value/register] with default stack
                value_str = parts[1].upper()
            
            if value_str in REGISTERS:
                # Push from register
                reg = REGISTERS[value_str]
                value = 0  # Not used when pushing from register
            else:
                # Push literal value
                value = parse_number(value_str) & 0xFF
                reg = 0xF  # No register
        elif len(parts) >= 2:
            # PUSH [value/register] (default stack)
            value_str = parts[1].upper()
            if value_str in REGISTERS:
                # Push from register
                reg = REGISTERS[value_str]
                value = 0  # Not used when pushing from register
            else:
                # Push literal value
                value = parse_number(value_str) & 0xFF
                reg = 0xF  # No register
        
        # According to opcodes.txt:
        # DATA3: type(2B)/stack(2B) - push type in upper 2 bits, stack type in lower 2 bits
        # DATA2: extype/reg - extype in upper nibble (0 for now), reg in lower nibble
        # DATA1: data - the value to push (if reg is 0xF) or unused (if pushing from register)
        data3 = (STACK_OPS['PUSH'] << 2) | stack_type
        data2 = (0 << 4) | reg  # extype=0, reg in lower nibble
        data1 = value
        return (data3, data2, data1, OPCODES['STACK'])

    # POP [stack name] [register name] - Pop value from stack to register
    if line.startswith('POP'):
        parts = line.split()
        # Default to GENERAL stack if not specified
        stack_type = STACK_TYPES['GENERAL']
        reg = None
        
        if len(parts) >= 3:
            # POP [stack name] [register]
            stack_name = parts[1].upper()
            if stack_name in STACK_TYPES:
                stack_type = STACK_TYPES[stack_name]
                reg = REGISTERS[parts[2]]
            else:
                # First argument is not a stack name, treat as POP [register] with default stack
                reg = REGISTERS[parts[1]]
        elif len(parts) >= 2:
            # POP [register] (default stack)
            reg = REGISTERS[parts[1]]
        
        if reg is None:
            raise ValueError(f"Invalid POP instruction: {line}")
        
        # According to opcodes.txt:
        # DATA3: type(2B)/stack(2B) - pop type in upper 2 bits, stack type in lower 2 bits
        # DATA2: extype/reg - extype in upper nibble (0 for now), reg in lower nibble
        # DATA1: data - unused for pop, set to 0
        data3 = (STACK_OPS['POP'] << 2) | stack_type
        data2 = (0 << 4) | reg  # extype=0, reg in lower nibble
        data1 = 0
        return (data3, data2, data1, OPCODES['STACK'])

    # SET R TO N
    if line.startswith('SET'):
        parts = line.split()
        if len(parts) < 4 or parts[2].upper() != 'TO':
            raise ValueError(f"SET instruction must be in format 'SET R TO N', got: {line}")
        if parts[1].upper() not in REGISTERS:
            raise ValueError(f"Invalid register in SET instruction: {parts[1]}")
        reg = REGISTERS[parts[1].upper()]
        value = parse_number(parts[-1])
        
        # DATA3: null, DATA2: register, DATA1: data/value
        return (0, reg, value & 0xFF, OPCODES['SET'])

    # STORE R INTO ADDR - Stores register into RAM address
    if line.startswith('STORE') and 'INTO' in line:
        parts = line.split()
        if parts[1].isalpha():  # STORE R INTO ADDR
            reg = REGISTERS[parts[1]]
            addr = parse_number(parts[3])
            # DATA3: null, DATA2: register, DATA1: address
            return (0, reg, addr & 0xFF, OPCODES['STORE'])
        else:  # STORE ADDR INTO R (LOAD)
            addr = parse_number(parts[1])
            reg = REGISTERS[parts[3]]
            # DATA3: null, DATA2: output register, DATA1: address
            return (0, reg, addr & 0xFF, OPCODES['LOAD'])

    # LOAD ADDR INTO R - Alternative syntax for STORE ADDR INTO R
    if line.startswith('LOAD'):
        parts = line.split()
        addr = parse_number(parts[1])
        reg = REGISTERS[parts[3]]
        # DATA3: null, DATA2: output register, DATA1: address
        return (0, reg, addr & 0xFF, OPCODES['LOAD'])

    # DRAW X Y R G B - can use registers or literal values
    if line.startswith('DRAW'):
        parts = line.split()
        
        # Validate parameter count
        if len(parts) != 6:
            raise ValueError(f"DRAW instruction requires exactly 5 parameters (X Y R G B), got {len(parts)-1}: {line}")
        
        # For DRAW instruction, if parameters are registers, we encode them differently
        # The hardware expects literal values, but if registers are used, we need to
        # handle this as a special case or expand it into multiple instructions
        
        # For now, let's assume all parameters are literal values
        # If they're registers, we'll use the register codes as placeholder values
        def parse_draw_param(param):
            param = param.upper()
            if param in REGISTERS:
                # Use register code as the value - this might need hardware support
                # or we might need to expand this into load + draw instructions
                return REGISTERS[param]
            else:
                return parse_number(param) & 0xF
        
        x = parse_draw_param(parts[1]) & 0xF  # 4-bit value
        y = parse_draw_param(parts[2]) & 0xF  # 4-bit value
        r = parse_draw_param(parts[3]) & 0xF  # 4-bit value
        g = parse_draw_param(parts[4]) & 0xF  # 4-bit value
        b = parse_draw_param(parts[5]) & 0xF  # 4-bit value
        
        # According to opcodes.txt:
        # DATA3: addrs/R/G/Btype(1B)/B - address in upper nibble, B in lower nibble
        # DATA2: R/G - R in upper nibble, G in lower nibble  
        # DATA1: addrs - Y in upper nibble, X in lower nibble
        data3 = (0 << 4) | b  # address=0 (not used for pixel drawing), B in lower nibble
        data2 = (r << 4) | g
        data1 = (y << 4) | x
        
        return (data3, data2, data1, OPCODES['DRAW'])

    # JUMP label IF condition
    if line.startswith('JUMP'):
        parts = line.split()
        label = parts[1]
        
        # Check for jump condition
        jump_type = None
        key = 0
        if len(parts) > 2:
            # Handle both "JUMP label IF condition" and "JUMP label condition" syntax
            condition_index = 3 if parts[2].upper() == 'IF' else 2
            if len(parts) > condition_index:
                condition = parts[condition_index].upper()
                if condition in JUMP_TYPES:
                    jump_type = condition
                elif condition == 'ZERO':
                    jump_type = 'ZERO'
                elif condition == 'CARRY':
                    jump_type = 'CARRY'
                elif condition == 'KEY':
                    jump_type = 'KEY'
                    if len(parts) > condition_index + 1:
                        key = KEYS.get(parts[condition_index + 1].upper(), 0)
                elif condition == 'ANY' and len(parts) > condition_index + 1 and parts[condition_index + 1].upper() == 'KEY':
                    # Handle "ANY KEY" case - use jump type 0xFF
                    jump_type = None  # Will be set to 0xFF below
                    key = 0
        
        # Determine target address - this is the instruction address where we want to jump
        addr = None
        is_function_call = False
        
        # Check if it's a line number (L1, L2, etc.)
        if line_labels and label.upper().startswith('L') and label.upper()[1:].isdigit():
            line_num = int(label.upper()[1:])
            if f'L{line_num}' in line_labels:
                addr = line_labels[f'L{line_num}']
            else:
                raise ValueError(f"Unknown line label: '{label}'. Available line labels: {', '.join(sorted(line_labels.keys()))}")
        # Check if it's a function call (with or without brackets)
        elif functions and label.upper() in functions:
            addr = functions[label.upper()]
            is_function_call = True
        elif functions and label.startswith('[') and label.endswith(']'):
            func_name = label[1:-1].upper()
            if func_name in functions:
                addr = functions[func_name]
                is_function_call = True
            elif func_name in fake_functions:
                # Fake function - we'll handle this as a function call too
                addr = 0  # Placeholder, will be expanded inline
                is_function_call = True
        # Check if it's a fake function without brackets
        elif fake_functions and label.upper() in fake_functions:
            addr = 0  # Placeholder, will be expanded inline
            is_function_call = True
        # Check if it's a regular label
        elif label.upper() in labels:
            addr = labels[label.upper()]
        else:
            # Try to parse it as a number (direct instruction address)
            try:
                addr = parse_number(label)
            except ValueError:
                raise ValueError(f"Unknown jump target: '{label}'. Make sure the label is defined with a colon (e.g., '{label}:') or use line syntax (e.g., 'L5').")
        
        # If it's a function call, we need to store the return address
        if is_function_call:
            # Find the function name
            func_name = None
            if label.startswith('[') and label.endswith(']'):
                func_name = label[1:-1].upper()
            else:
                func_name = label.upper()
            
            # Return a special marker for function calls that the assembler will expand
            return ('FUNCTION_CALL', func_name, 0, jump_type, key)
        
        # Handle special case for "ANY KEY"
        if ((len(parts) > 4 and parts[3].upper() == 'ANY' and parts[4].upper() == 'KEY') or
            (len(parts) > 3 and parts[2].upper() == 'ANY' and parts[3].upper() == 'KEY')):
            jt = JUMP_TYPES['KEY']  # Use KEY jump type
            key = KEYS['ANY']  # Use ANY key value (0xF)
        else:
            jt = JUMP_TYPES.get(jump_type, 0)
        
        # According to opcodes.txt:
        # DATA3: usereg(1B)/key - usereg in upper nibble, key in lower nibble
        # DATA2: reg(4B)/jmptype(4B) - reg in upper nibble, jump type in lower nibble
        # DATA1: addrs - the instruction address to jump to (resolved from label)
        if addr is None:
            raise ValueError(f"Could not resolve instruction address for jump target: '{label}'")
        
        usereg = 0  # 0 = use address directly, 1 = use register
        reg = 0     # register to use if usereg=1
        
        # Add 1 to the jump address to fix hardware issue
        addr = (addr + 1) & 0xFF
        
        data3 = (usereg << 4) | (key & 0xF)
        data2 = (reg << 4) | (jt & 0xF)
        data1 = addr & 0xFF
        
        return (data3, data2, data1, OPCODES['JUMP'])

    # ALU binary op: A + B = C or C = A + B
    # Format 1: val1 op val2 = outreg
    match = re.match(r'([A-Z0-9\[\]]+)\s*([+*/]|NAND|AND|OR|NOR|XOR|XNOR|COMPARE|COMPARE_SIGNED|-)\s*([A-Z0-9\[\]]+)\s*=\s*([A-Z]+)', line)
    if match:
        val1_str, op, val2_str, outreg = match.groups()
        
        # Parse the values which could be registers, constants, or memory addresses
        val1_type, val1 = parse_value(val1_str)
        val2_type, val2 = parse_value(val2_str)
        
        # Check if both operands are RAM addresses - not allowed
        if val1_type == VALUE_TYPES['RAM'] and val2_type == VALUE_TYPES['RAM']:
            raise ValueError(f"Cannot use two RAM addresses in a single operation: {line}")
        
        opnum = ALU_OPS[op]
        out = REGISTERS[outreg]
        
        # According to opcodes.txt:
        # DATA3: reg1nibble/op - reg1 in upper nibble, op in lower nibble
        # DATA2: reg2Type(2B)/reg1Type(2B)/outReg(4B)
        # DATA1: reg2/1 - reg2 in upper nibble, reg1 in lower nibble
        
        # For ALU operations, reg1nibble refers to the first operand's register/value
        # If val1 is a register, use its register code; otherwise use the value itself
        reg1_nibble = val1 if val1_type == VALUE_TYPES['REGISTER'] else val1 & 0xF
        
        data3 = (reg1_nibble << 4) | (opnum & 0xF)
        data2 = (val2_type << 6) | (val1_type << 4) | out
        data1 = ((val2 & 0xF) << 4) | (val1 & 0xF)
        
        return (data3, data2, data1, OPCODES['ALU'])

    # Format 2: outreg = val1 op val2
    match = re.match(r'([A-Z]+)\s*=\s*([A-Z0-9\[\]]+)\s*([+*/]|NAND|AND|OR|NOR|XOR|XNOR|COMPARE|COMPARE_SIGNED|-)\s*([A-Z0-9\[\]]+)', line)
    if match:
        outreg, val1_str, op, val2_str = match.groups()
        
        # Parse the values which could be registers, constants, or memory addresses
        val1_type, val1 = parse_value(val1_str)
        val2_type, val2 = parse_value(val2_str)
        
        # Check if both operands are RAM addresses - not allowed
        if val1_type == VALUE_TYPES['RAM'] and val2_type == VALUE_TYPES['RAM']:
            raise ValueError(f"Cannot use two RAM addresses in a single operation: {line}")
        
        opnum = ALU_OPS[op]
        out = REGISTERS[outreg]
        
        # For ALU operations, reg1nibble refers to the first operand's register/value
        # If val1 is a register, use its register code; otherwise use the value itself
        reg1_nibble = val1 if val1_type == VALUE_TYPES['REGISTER'] else val1 & 0xF
        
        data3 = (reg1_nibble << 4) | (opnum & 0xF)
        data2 = (val2_type << 6) | (val1_type << 4) | out
        data1 = ((val2 & 0xF) << 4) | (val1 & 0xF)
        
        return (data3, data2, data1, OPCODES['ALU'])

    # ALU unary op: NOT A = B or B = NOT A
    # Format 1: NOT val1 = outreg
    match = re.match(r'(NOT)\s+([A-Z0-9\[\]]+)\s*=\s*([A-Z]+)', line)
    if match:
        op, val1_str, outreg = match.groups()
        
        # Parse the value which could be a register, constant, or memory address
        val1_type, val1 = parse_value(val1_str)
        
        opnum = ALU_OPS[op]
        val2 = 0  # unused input for NOT operation
        val2_type = VALUE_TYPES['REGISTER']  # Doesn't matter for NOT
        out = REGISTERS[outreg]
        
        # For ALU operations, reg1nibble refers to the first operand's register/value
        # If val1 is a register, use its register code; otherwise use the value itself
        reg1_nibble = val1 if val1_type == VALUE_TYPES['REGISTER'] else val1 & 0xF
        
        data3 = (reg1_nibble << 4) | (opnum & 0xF)
        data2 = (val2_type << 6) | (val1_type << 4) | out
        data1 = ((val2 & 0xF) << 4) | (val1 & 0xF)
        
        return (data3, data2, data1, OPCODES['ALU'])

    # Format 2: outreg = NOT val1
    match = re.match(r'([A-Z]+)\s*=\s*(NOT)\s+([A-Z0-9\[\]]+)', line)
    if match:
        outreg, op, val1_str = match.groups()
        
        # Parse the value which could be a register, constant, or memory address
        val1_type, val1 = parse_value(val1_str)
        
        opnum = ALU_OPS[op]
        val2 = 0  # unused input for NOT operation
        val2_type = VALUE_TYPES['REGISTER']  # Doesn't matter for NOT
        out = REGISTERS[outreg]
        
        # For ALU operations, reg1nibble refers to the first operand's register/value
        # If val1 is a register, use its register code; otherwise use the value itself
        reg1_nibble = val1 if val1_type == VALUE_TYPES['REGISTER'] else val1 & 0xF
        
        data3 = (reg1_nibble << 4) | (opnum & 0xF)
        data2 = (val2_type << 6) | (val1_type << 4) | out
        data1 = ((val2 & 0xF) << 4) | (val1 & 0xF)
        
        return (data3, data2, data1, OPCODES['ALU'])

    # IF statement - this is a fake instruction that gets expanded
    if line.startswith('IF '):
        return ('IF_STATEMENT', line, 0, 'SPECIAL')
    
    # ELSE and END are also fake instructions
    if line == 'ELSE':
        return ('ELSE', 0, 0, 'SPECIAL')
    
    if line == 'END':
        return ('END', 0, 0, 'SPECIAL')
    
    raise ValueError(f"Unknown instruction: {line}")

def expand_fake_function(fake_func_instructions, labels, functions, line_labels, call_stack_ptr, fake_functions, functions_with_return=None):
    """Expand fake function instructions inline"""
    if functions_with_return is None:
        functions_with_return = set()
    
    expanded = []
    for instruction in fake_func_instructions:
        result = parse_instruction(instruction, labels, functions, line_labels, call_stack_ptr, fake_functions)
        if result:
            if result[0] == 'FUNCTION_CALL':
                # Handle function calls in fake functions
                _, func_name, stack_ptr, jump_type, key = result
                if func_name and func_name in fake_functions:
                    # For fake function calls within fake functions, treat as regular jumps
                    # The label should already be set up to point to the expanded location
                    jt = JUMP_TYPES.get(jump_type, 0) if jump_type else 0
                    if func_name in labels:
                        expanded.append((key or 0, jt, (labels[func_name] + 1) & 0xFF, OPCODES['JUMP']))
                    else:
                        # If not found in labels, this might be a forward reference or error
                        expanded.append((key or 0, jt, 1, OPCODES['JUMP']))
                else:
                    # Real function call - only use D register if function has RETURN
                    expanded.extend(generate_function_call_instructions(func_name, jump_type, key, functions_with_return))
            elif result[0] == 'RETURN':
                # Generate return instructions
                expanded.extend(generate_return_instructions())
            else:
                expanded.append(result)
    return expanded

def generate_function_call_instructions(target_addr, jump_type=None, key=0, functions_with_return=None):
    """Generate instructions for function call with return address storage"""
    if functions_with_return is None:
        functions_with_return = set()
    
    instructions = []
    
    # Only store return address if the target function has a RETURN statement
    if target_addr in functions_with_return:
        # Store return address to return stack
        # We'll need to calculate the actual return address during final assembly
        # For now, we'll use a placeholder that gets resolved later
        instructions.append(('STORE_RETURN_ADDR', 0, 0, 'SPECIAL'))
    
    # Jump to function - add 1 to the target address
    jt = JUMP_TYPES.get(jump_type, 0) if jump_type else 0
    instructions.append((key, jt, (target_addr + 1) & 0xFF, OPCODES['JUMP']))
    
    return instructions

def generate_return_instructions():
    """Generate instructions for return from function"""
    instructions = []
    
    # Pop return address from return stack and jump to it
    instructions.append(('LOAD_RETURN_ADDR', 0, 0, 'SPECIAL'))
    
    return instructions

def parse_if_condition(condition_str):
    """Parse an if condition and return the comparison operation and operands"""
    condition_str = condition_str.strip()
    
    # Support different comparison operators
    comparison_ops = ['==', '!=', '<=', '>=', '<', '>']
    
    for op in comparison_ops:
        if op in condition_str:
            parts = condition_str.split(op, 1)
            if len(parts) == 2:
                left = parts[0].strip()
                right = parts[1].strip()
                return left, op, right
    
    raise ValueError(f"Invalid if condition: {condition_str}")

def generate_if_instructions(if_line, then_instructions, else_instructions, labels, instruction_address):
    """Generate instructions for if statement"""
    instructions = []
    
    # Parse the if condition
    # Expected format: "IF [value1] [comparison] [value2] THEN"
    if_parts = if_line.upper().split()
    if len(if_parts) < 4 or if_parts[-1] != 'THEN':
        raise ValueError(f"Invalid if statement format: {if_line}")
    
    # Extract condition part (everything between IF and THEN)
    condition_part = ' '.join(if_parts[1:-1])
    
    try:
        left_val, op, right_val = parse_if_condition(condition_part)
    except ValueError:
        raise ValueError(f"Invalid if condition in: {if_line}")
    
    # Parse the operands
    left_type, left = parse_value(left_val)
    right_type, right = parse_value(right_val)
    
    # Check if both operands are RAM addresses - not allowed
    if left_type == VALUE_TYPES['RAM'] and right_type == VALUE_TYPES['RAM']:
        raise ValueError(f"Cannot compare two RAM addresses: {condition_part}")
    
    # Generate unique labels for this if statement
    import time
    unique_id = int(time.time() * 1000000) % 1000000  # Use microseconds for uniqueness
    else_label = f"_IF_ELSE_{unique_id}"
    end_label = f"_IF_END_{unique_id}"
    then_label = f"_IF_THEN_{unique_id}"
    
    # Determine the comparison operation and jump condition
    # We'll use COMPARE operation and then jump based on the result
    compare_op = 'COMPARE'  # Use unsigned compare by default
    
    # For ALU operations, reg1nibble refers to the first operand's register/value
    reg1_nibble = left if left_type == VALUE_TYPES['REGISTER'] else left & 0xF
    
    # Generate comparison instruction (left COMPARE right = X register for result)
    # We'll use register X (0b100) to store the comparison result
    result_reg = REGISTERS['X']
    opnum = ALU_OPS[compare_op]
    
    data3 = (reg1_nibble << 4) | (opnum & 0xF)
    data2 = (right_type << 6) | (left_type << 4) | result_reg
    data1 = (right << 4) | left
    
    instructions.append((data3, data2, data1, OPCODES['ALU']))
    
    # Calculate addresses for labels (we need to do this before creating jump instructions)
    instruction_count = 1  # We already have the compare instruction
    
    # Now generate the conditional jump based on the comparison operator
    # COMPARE sets flags: zero flag if equal, carry flag if left < right
    
    if op == '==':
        # Jump to then block if zero (equal), otherwise fall through to else
        instruction_count += 1  # Jump to then
        instruction_count += 1  # Jump to else (unconditional)
        
        then_addr = instruction_address + instruction_count
        labels[then_label] = then_addr
        
        instruction_count += len(then_instructions)
        if else_instructions:
            instruction_count += 1  # Jump over else block
        
        else_addr = instruction_address + instruction_count
        labels[else_label] = else_addr
        
        instruction_count += len(else_instructions)
        end_addr = instruction_address + instruction_count
        labels[end_label] = end_addr
        
        # Generate the actual jump instructions
        instructions.append((0, JUMP_TYPES['ZERO'], (then_addr + 1) & 0xFF, OPCODES['JUMP']))
        instructions.append((0, JUMP_TYPES[None], (else_addr + 1) & 0xFF, OPCODES['JUMP']))
        
        # Then block
        instructions.extend(then_instructions)
        
        # Jump over else block
        if else_instructions:
            instructions.append((0, JUMP_TYPES[None], (end_addr + 1) & 0xFF, OPCODES['JUMP']))
    
    elif op == '!=':
        # Jump to else if zero (equal), fall through to then if not equal
        instruction_count += 1  # Jump to else
        
        then_addr = instruction_address + instruction_count
        labels[then_label] = then_addr
        
        instruction_count += len(then_instructions)
        if else_instructions:
            instruction_count += 1  # Jump over else block
        
        else_addr = instruction_address + instruction_count
        labels[else_label] = else_addr
        
        instruction_count += len(else_instructions)
        end_addr = instruction_address + instruction_count
        labels[end_label] = end_addr
        
        # Generate the actual jump instructions
        instructions.append((0, JUMP_TYPES['ZERO'], (else_addr + 1) & 0xFF, OPCODES['JUMP']))
        
        # Then block (fall through)
        instructions.extend(then_instructions)
        
        # Jump over else block
        if else_instructions:
            instructions.append((0, JUMP_TYPES[None], (end_addr + 1) & 0xFF, OPCODES['JUMP']))
    
    elif op == '<':
        # Jump to then if carry (left < right), otherwise fall through to else
        instruction_count += 1  # Jump to then
        instruction_count += 1  # Jump to else (unconditional)
        
        then_addr = instruction_address + instruction_count
        labels[then_label] = then_addr
        
        instruction_count += len(then_instructions)
        if else_instructions:
            instruction_count += 1  # Jump over else block
        
        else_addr = instruction_address + instruction_count
        labels[else_label] = else_addr
        
        instruction_count += len(else_instructions)
        end_addr = instruction_address + instruction_count
        labels[end_label] = end_addr
        
        # Generate the actual jump instructions
        instructions.append((0, JUMP_TYPES['CARRY'], (then_addr + 1) & 0xFF, OPCODES['JUMP']))
        instructions.append((0, JUMP_TYPES[None], (else_addr + 1) & 0xFF, OPCODES['JUMP']))
        
        # Then block
        instructions.extend(then_instructions)
        
        # Jump over else block
        if else_instructions:
            instructions.append((0, JUMP_TYPES[None], (end_addr + 1) & 0xFF, OPCODES['JUMP']))
    
    elif op == '>=':
        # Jump to else if carry (left < right), fall through to then if not
        instruction_count += 1  # Jump to else
        
        then_addr = instruction_address + instruction_count
        labels[then_label] = then_addr
        
        instruction_count += len(then_instructions)
        if else_instructions:
            instruction_count += 1  # Jump over else block
        
        else_addr = instruction_address + instruction_count
        labels[else_label] = else_addr
        
        instruction_count += len(else_instructions)
        end_addr = instruction_address + instruction_count
        labels[end_label] = end_addr
        
        # Generate the actual jump instructions
        instructions.append((0, JUMP_TYPES['CARRY'], (else_addr + 1) & 0xFF, OPCODES['JUMP']))
        
        # Then block (fall through)
        instructions.extend(then_instructions)
        
        # Jump over else block
        if else_instructions:
            instructions.append((0, JUMP_TYPES[None], (end_addr + 1) & 0xFF, OPCODES['JUMP']))
    
    elif op == '>':
        # left > right means NOT(left <= right) means NOT(left < right OR left == right)
        # Jump to else if zero (equal) or carry (less than)
        instruction_count += 1  # Jump to else on zero
        instruction_count += 1  # Jump to else on carry
        
        then_addr = instruction_address + instruction_count
        labels[then_label] = then_addr
        
        instruction_count += len(then_instructions)
        if else_instructions:
            instruction_count += 1  # Jump over else block
        
        else_addr = instruction_address + instruction_count
        labels[else_label] = else_addr
        
        instruction_count += len(else_instructions)
        end_addr = instruction_address + instruction_count
        labels[end_label] = end_addr
        
        # Generate the actual jump instructions
        instructions.append((0, JUMP_TYPES['ZERO'], (else_addr + 1) & 0xFF, OPCODES['JUMP']))
        instructions.append((0, JUMP_TYPES['CARRY'], (else_addr + 1) & 0xFF, OPCODES['JUMP']))
        
        # Then block (fall through)
        instructions.extend(then_instructions)
        
        # Jump over else block
        if else_instructions:
            instructions.append((0, JUMP_TYPES[None], (end_addr + 1) & 0xFF, OPCODES['JUMP']))
    
    elif op == '<=':
        # left <= right means left < right OR left == right
        # Jump to then if zero (equal) or carry (less than)
        instruction_count += 1  # Jump to then on zero
        instruction_count += 1  # Jump to then on carry
        instruction_count += 1  # Jump to else (unconditional)
        
        then_addr = instruction_address + instruction_count
        labels[then_label] = then_addr
        
        instruction_count += len(then_instructions)
        if else_instructions:
            instruction_count += 1  # Jump over else block
        
        else_addr = instruction_address + instruction_count
        labels[else_label] = else_addr
        
        instruction_count += len(else_instructions)
        end_addr = instruction_address + instruction_count
        labels[end_label] = end_addr
        
        # Generate the actual jump instructions
        instructions.append((0, JUMP_TYPES['ZERO'], (then_addr + 1) & 0xFF, OPCODES['JUMP']))
        instructions.append((0, JUMP_TYPES['CARRY'], (then_addr + 1) & 0xFF, OPCODES['JUMP']))
        instructions.append((0, JUMP_TYPES[None], (else_addr + 1) & 0xFF, OPCODES['JUMP']))
        
        # Then block
        instructions.extend(then_instructions)
        
        # Jump over else block
        if else_instructions:
            instructions.append((0, JUMP_TYPES[None], (end_addr + 1) & 0xFF, OPCODES['JUMP']))
    
    # Else block (if it exists)
    if else_instructions:
        instructions.extend(else_instructions)
    
    return instructions

def assemble(filepath):
    with open(filepath, 'r') as f:
        lines = f.readlines()

    # First pass to identify functions and fake functions only
    functions, fake_functions, functions_with_return = first_pass(lines)
    
    # Initialize instruction address tracking
    labels = {}
    line_labels = {}  # For L1, L2, etc.
    instruction_address = 0  # Track actual instruction addresses (not source line addresses)
    
    # Initialize call stack pointer
    call_stack_ptr = {'current': STACK_START}
    
    # Separate main program from functions
    main_lines = []
    function_lines = {}
    current_function = None
    
    for i, line in enumerate(lines):
        clean_line = re.sub(r'//.*', '', line).strip()
        if not clean_line:
            continue
            
        # Check if this is a function definition
        if ':' in clean_line:
            label_part = clean_line.split(':')[0].strip()
            if label_part.startswith('[') and label_part.endswith(']'):
                func_name = label_part[1:-1].upper()
                if func_name not in fake_functions:  # Real function
                    current_function = func_name
                    function_lines[func_name] = []
                else:
                    # Fake function - don't process it
                    current_function = None
                continue
            else:
                # Check if it's a fake function without brackets
                rest_of_line = clean_line.split(':', 1)[1].strip().upper()
                if rest_of_line == 'FAKE':
                    # Fake function - don't process it
                    current_function = None
                    continue
                else:
                    # Check if this is a function (already detected in first pass)
                    func_name = label_part.upper()
                    if func_name in functions:
                        current_function = func_name
                        function_lines[func_name] = []
                        continue
                    else:
                        # Regular label
                        current_function = None
        
        # Add line to appropriate section
        if current_function:
            function_lines[current_function].append(line)
        else:
            main_lines.append(line)
    
    # Process main program with proper instruction address tracking
    main_instrs = []
    # Start with 2 NOPs as per original code
    main_instrs.append((0, 0, 0, OPCODES['NOP']))
    main_instrs.append((0, 0, 0, OPCODES['NOP']))
    instruction_address = 2  # Account for the 2 initial NOPs

    # First pass: Create line labels for ALL source lines and collect labels with their instruction addresses
    temp_address = instruction_address
    in_main_program = True
    current_function_start = None
    
    # Create line labels for ALL source lines (L1, L2, etc.) - this must cover the entire file
    for i, line in enumerate(lines):
        clean_line = re.sub(r'//.*', '', line).strip()
        
        # Assign line label to current instruction address (for lines that will generate instructions)
        if clean_line and not clean_line.endswith(': fake'):
            # For lines that generate instructions, assign the line label to the current address
            if not ':' in clean_line or (':' in clean_line and clean_line.split(':', 1)[1].strip()):
                # This line will generate an instruction, so assign the line label
                line_labels[f'L{i+1}'] = temp_address
        else:
            # For empty lines or comments, assign the line label to current address (no instruction will be generated)
            line_labels[f'L{i+1}'] = temp_address
        
        # Only increment address for lines that generate actual instructions
        if clean_line and not clean_line.endswith(': fake'):
            # Check if this is a label definition
            if ':' in clean_line:
                label_part = clean_line.split(':')[0].strip()
                rest_of_line = clean_line.split(':', 1)[1].strip().upper() if len(clean_line.split(':', 1)) > 1 else ''
                
                # Skip fake functions
                if rest_of_line == 'FAKE':
                    continue
                    
                # Check if this is a function definition
                is_function_def = ((label_part.startswith('[') and label_part.endswith(']') and label_part[1:-1].upper() in functions) or
                                 label_part.upper() in functions)
                
                if is_function_def:
                    in_main_program = False
                    current_function_start = temp_address
                
                # Labels should point to the next instruction that will be executed
                # We need to find where the next actual instruction will be placed
                next_instruction_addr = temp_address
                
                # Look ahead to find the next line that generates an instruction
                for j in range(i + 1, len(lines)):
                    next_line = re.sub(r'//.*', '', lines[j]).strip()
                    if next_line and not next_line.endswith(': fake'):
                        # Check if this is another label definition
                        if ':' in next_line:
                            next_label_part = next_line.split(':')[0].strip()
                            next_rest_of_line = next_line.split(':', 1)[1].strip().upper() if len(next_line.split(':', 1)) > 1 else ''
                            if next_rest_of_line == 'FAKE':
                                continue
                            # If it's another label, continue looking (labels don't increment address)
                            continue
                        else:
                            # Found the next actual instruction
                            break
                
                # Record all labels and their instruction addresses
                if label_part.startswith('[') and label_part.endswith(']'):
                    labels[label_part[1:-1].upper()] = next_instruction_addr
                else:
                    labels[label_part.upper()] = next_instruction_addr
                
                # Don't increment temp_address for label definitions - the next instruction will use this address
                continue
            
            # Count actual instruction increments
            instruction_increment = count_instruction_increment(clean_line, fake_functions, functions_with_return)
            temp_address += instruction_increment
    
    # Second pass: Generate instructions for main program
    i = 0
    while i < len(lines):
        line = lines[i]
        clean_line = re.sub(r'//.*', '', line).strip()
        
        if not clean_line:
            i += 1
            continue
        
        # Check if we've hit a function definition - if so, stop processing main program
        if ':' in clean_line:
            label_part = clean_line.split(':')[0].strip()
            rest_of_line = clean_line.split(':', 1)[1].strip().upper() if len(clean_line.split(':', 1)) > 1 else ''
            
            # Skip fake functions
            if rest_of_line == 'FAKE':
                i += 1
                continue
                
            # Skip function definitions (they're handled separately)
            if ((label_part.startswith('[') and label_part.endswith(']') and label_part[1:-1].upper() in functions) or
                label_part.upper() in functions):
                # We've hit a real function, stop processing main program
                break
            
            # Regular label - skip, already processed in first pass
            i += 1
            continue
        
        # Check for IF statement
        if clean_line.upper().startswith('IF '):
            # Collect the entire if block
            if_line = clean_line
            then_instructions = []
            else_instructions = []
            
            # Find the matching ELSE and END
            j = i + 1
            in_else = False
            nesting_level = 0
            
            while j < len(lines):
                block_line = re.sub(r'//.*', '', lines[j]).strip()
                if not block_line:
                    j += 1
                    continue
                
                block_line_upper = block_line.upper()
                
                # Track nesting level for nested if statements
                if block_line_upper.startswith('IF '):
                    nesting_level += 1
                elif block_line_upper == 'END':
                    if nesting_level == 0:
                        # This is our matching END
                        break
                    else:
                        nesting_level -= 1
                elif block_line_upper == 'ELSE' and nesting_level == 0:
                    # This is our matching ELSE
                    in_else = True
                    j += 1
                    continue
                
                # Add instruction to appropriate block
                if in_else:
                    else_instructions.append(block_line)
                else:
                    then_instructions.append(block_line)
                
                j += 1
            
            if j >= len(lines):
                raise ValueError(f"IF statement starting at line {i+1} has no matching END")
            
            # Parse the then and else instructions
            parsed_then = []
            for instr in then_instructions:
                try:
                    result = parse_instruction(instr, labels, functions, line_labels, call_stack_ptr, fake_functions)
                    if result:
                        parsed_then.append(result)
                except ValueError as e:
                    print(f"Error in IF then block: {e}")
                    sys.exit(1)
            
            parsed_else = []
            for instr in else_instructions:
                try:
                    result = parse_instruction(instr, labels, functions, line_labels, call_stack_ptr, fake_functions)
                    if result:
                        parsed_else.append(result)
                except ValueError as e:
                    print(f"Error in IF else block: {e}")
                    sys.exit(1)
            
            # Generate the if statement instructions
            try:
                if_instrs = generate_if_instructions(if_line, parsed_then, parsed_else, labels, instruction_address)
                main_instrs.extend(if_instrs)
                instruction_address += len(if_instrs)
            except ValueError as e:
                print(f"Error generating IF statement: {e}")
                sys.exit(1)
            
            # Skip to after the END
            i = j + 1
            continue
        
        try:
            result = parse_instruction(line, labels, functions, line_labels, call_stack_ptr, fake_functions)
            if result:
                if result[0] == 'FUNCTION_CALL':
                    # Handle function call
                    _, func_name, _, jump_type, key = result
                    
                    if func_name and func_name in fake_functions:
                        # Expand fake function inline
                        # Mark the start position for potential recursive jumps
                        labels[func_name] = instruction_address
                        
                        expanded = expand_fake_function(fake_functions[func_name]['instructions'], 
                                                     labels, functions, line_labels, call_stack_ptr, fake_functions, functions_with_return)
                        main_instrs.extend(expanded)
                        instruction_address += len(expanded)
                    else:
                        # Regular function call - only use D register if function has RETURN
                        if func_name in functions_with_return:
                            # Function has RETURN - store location for return address calculation
                            call_location = instruction_address
                            
                            # Store return address (will be resolved later)
                            main_instrs.append(('STORE_RETURN_ADDR', call_location, 0, 'SPECIAL'))
                            instruction_address += 2  # STORE_RETURN_ADDR generates 2 instructions
                        
                        # Jump to function (store function name for later resolution)
                        jt = JUMP_TYPES.get(jump_type, 0) if jump_type else 0
                        main_instrs.append((key, jt, func_name, OPCODES['JUMP']))  # Store function name
                        instruction_address += 1
                elif result[0] in ['IF_STATEMENT', 'ELSE', 'END']:
                    # These should have been handled above, skip them
                    pass
                else:
                    main_instrs.append(result)
                    # Increment instruction_address based on instruction type
                    if result and len(result) == 4 and result[0] == 'RETURN':
                        instruction_address += 2  # RETURN generates 2 instructions
                    else:
                        instruction_address += 1  # Most instructions generate 1 instruction
        except ValueError as e:
            print(f"Error: {e}")
            sys.exit(1)
        
        i += 1

    # Ensure program ends with HALT
    if not main_instrs or main_instrs[-1][3] != OPCODES['HALT']:
        main_instrs.append((0, 0, 0, OPCODES['HALT']))
        instruction_address += 1
    
    # The labels and functions should already be set from the first pass above
    # Now we just need to ensure function addresses are properly stored
    for func_name in functions:
        if func_name in labels:
            functions[func_name] = labels[func_name]
    
    # Now resolve function call addresses in main program
    for i, instr in enumerate(main_instrs):
        if len(instr) == 4 and instr[3] == OPCODES['JUMP'] and isinstance(instr[2], str):  # Function call jump with function name
            func_name = instr[2]
            if func_name in functions and functions[func_name] is not None:
                # Update the jump address to the resolved instruction address
                main_instrs[i] = (instr[0], instr[1], (functions[func_name] + 1) & 0xFF, instr[3])
    
    # Add functions after main program
    function_instrs = []
    function_instruction_address = len(main_instrs)  # Functions start after main program
    
    for func_name, func_lines in function_lines.items():
        if func_name not in fake_functions:  # Don't add fake functions to final code
            for line in func_lines:
                try:
                    result = parse_instruction(line, labels, functions, line_labels, call_stack_ptr, fake_functions)
                    if result:
                        if result[0] == 'RETURN':
                            # Generate return instructions - will be resolved later
                            function_instrs.append(('RETURN', 0, 0, 'SPECIAL'))
                            function_instruction_address += 2  # RETURN generates 2 instructions
                        elif result[0] == 'FUNCTION_CALL':
                            # Handle function call within function
                            _, func_name, _, jump_type, key = result
                            
                            if func_name and func_name in fake_functions:
                                # Expand fake function inline
                                expanded = expand_fake_function(fake_functions[func_name]['instructions'], 
                                                             labels, functions, line_labels, call_stack_ptr, fake_functions, functions_with_return)
                                function_instrs.extend(expanded)
                                function_instruction_address += len(expanded)
                            else:
                                # Regular function call - only use D register if function has RETURN
                                if func_name in functions_with_return:
                                    # Function has RETURN - store location for return address calculation
                                    call_location = function_instruction_address
                                    
                                    # Store return address (will be resolved later)
                                    function_instrs.append(('STORE_RETURN_ADDR', call_location, 0, 'SPECIAL'))
                                    function_instruction_address += 2  # STORE_RETURN_ADDR generates 2 instructions
                                
                                # Jump to function
                                jt = JUMP_TYPES.get(jump_type, 0) if jump_type else 0
                                if func_name in functions and functions[func_name] is not None:
                                    function_instrs.append((key, jt, (functions[func_name] + 1) & 0xFF, OPCODES['JUMP']))
                                else:
                                    function_instrs.append((key, jt, func_name, OPCODES['JUMP']))  # Store function name for later resolution
                                function_instruction_address += 1
                        else:
                            function_instrs.append(result)
                            function_instruction_address += 1
                except ValueError as e:
                    print(f"Error in function {func_name}: {e}")
                    sys.exit(1)
    
    # Resolve function call addresses in function instructions
    for i, instr in enumerate(function_instrs):
        if len(instr) == 4 and instr[3] == OPCODES['JUMP'] and isinstance(instr[2], str):  # Function call jump with function name
            func_name = instr[2]
            if func_name in functions and functions[func_name] is not None:
                # Update the jump address to the resolved instruction address
                function_instrs[i] = (instr[0], instr[1], (functions[func_name] + 1) & 0xFF, instr[3])
    
    # Combine main program and functions
    all_instrs = main_instrs + function_instrs
    
    # Resolve STORE_RETURN_ADDR and RETURN instructions
    resolved_instrs = []
    for i, instr in enumerate(all_instrs):
        if len(instr) > 3 and instr[3] == 'SPECIAL':
            if instr[0] == 'STORE_RETURN_ADDR':
                # Store return address (instruction after the jump)
                # The return address should be the next instruction after this function call
                return_addr = len(resolved_instrs) + 2  # After SET D and PUSH RETURN D
                
                # Generate: SET D TO return_addr, PUSH RETURN D
                resolved_instrs.append((0, REGISTERS['D'], return_addr & 0xFF, OPCODES['SET']))
                # PUSH RETURN D (push register D to RETURN stack)
                data3 = (STACK_OPS['PUSH'] << 2) | STACK_TYPES['RETURN']
                data2 = (0 << 4) | REGISTERS['D']  # extype=0, reg=D
                resolved_instrs.append((data3, data2, 0, OPCODES['STACK']))
            elif instr[0] == 'RETURN':
                # Return instruction: Pop return address from stack and jump to it
                
                # Generate: POP RETURN D, JUMP D
                # POP RETURN D (pop from RETURN stack to register D)
                data3 = (STACK_OPS['POP'] << 2) | STACK_TYPES['RETURN']
                data2 = (0 << 4) | REGISTERS['D']  # extype=0, reg=D
                resolved_instrs.append((data3, data2, 0, OPCODES['STACK']))
                # JUMP D (jump to address in register D)
                data3 = (1 << 4) | 0  # usereg=1, key=0
                data2 = (REGISTERS['D'] << 4) | 0  # reg=D, jmptype=0 (unconditional)
                resolved_instrs.append((data3, data2, 0, OPCODES['JUMP']))
        else:
            resolved_instrs.append(instr)
    
    # Pad to 256 instructions
    while len(resolved_instrs) < 256:
        resolved_instrs.append((0, 0, 0, OPCODES['NOP']))

    # Format for ROM1 and ROM2
    # ROM1 contains DATA1 + OPCODE
    # ROM2 contains DATA3 + DATA2
    
    rom1 = []
    rom2 = []
    for instr in resolved_instrs:
        if len(instr) == 4:
            data3, data2, data1, opcode = instr
            # Validate data fields are within valid ranges
            if not (0 <= data3 <= 255):
                raise ValueError(f"DATA3 field out of range (0-255): {data3} in instruction {instr}")
            if not (0 <= data2 <= 255):
                raise ValueError(f"DATA2 field out of range (0-255): {data2} in instruction {instr}")
            if not (0 <= data1 <= 255):
                raise ValueError(f"DATA1 field out of range (0-255): {data1} in instruction {instr}")
            if not (0 <= opcode <= 255):
                raise ValueError(f"OPCODE field out of range (0-255): {opcode} in instruction {instr}")
            
            rom1.append(f"{data1:08b}{opcode:08b}")
            rom2.append(f"{data3:08b}{data2:08b}")
        else:
            raise ValueError(f"ERROR: Malformed instruction detected: {instr}. All instructions must be 4-tuples (data3, data2, data1, opcode).")
    
    
        # Find the last non-NOP instruction
    last_real_instruction = len(rom1) - 1
    while last_real_instruction >= 0:
        opcode = int(rom1[last_real_instruction][8:], 2)
        if opcode != OPCODES['NOP']:
            break
        last_real_instruction -= 1
    
    # Write to outputBinary.txt with ROM2 and ROM1 combined on each line, plus human-readable instruction
    with open("outputBinary.txt", "w") as f:
        for i, (r2, r1) in enumerate(zip(rom2, rom1)):
            # Skip trailing NOPs
            if i > last_real_instruction:
                break
                
            # Parse the binary data back into components
            data3 = int(r2[:8], 2)
            data2 = int(r2[8:], 2)
            data1 = int(r1[:8], 2)
            opcode = int(r1[8:], 2)
            
            # Generate human-readable instruction based on opcode
            instruction = ""
            if opcode == OPCODES['NOP']:
                instruction = "NOP"
            elif opcode == OPCODES['HALT']:
                instruction = "HALT"
            elif opcode == OPCODES['ALU']:
                # Extract components from data fields
                reg1_nibble = (data3 >> 4) & 0xF
                op_code = data3 & 0xF
                reg2_type = (data2 >> 6) & 0x3
                reg1_type = (data2 >> 4) & 0x3
                out_reg = data2 & 0xF
                reg2 = (data1 >> 4) & 0xF
                reg1 = data1 & 0xF
                
                # Get register names
                reg_names = {v: k for k, v in REGISTERS.items()}
                out_reg_name = reg_names.get(out_reg, f"R{out_reg}")
                
                # Get operation name
                op_names = {v: k for k, v in ALU_OPS.items()}
                op_name = op_names.get(op_code, f"OP{op_code}")
                
                # Format operands based on type
                def format_operand(reg_val, reg_type):
                    if reg_type == VALUE_TYPES['REGISTER']:
                        return reg_names.get(reg_val, f"R{reg_val}")
                    elif reg_type == VALUE_TYPES['BUILTIN']:
                        return str(reg_val)
                    elif reg_type == VALUE_TYPES['RAM']:
                        return f"[{reg_val}]"
                    return f"?{reg_val}"
                
                reg1_str = format_operand(reg1, reg1_type)
                reg2_str = format_operand(reg2, reg2_type)
                
                instruction = f"{out_reg_name} = {reg1_str} {op_name} {reg2_str}"
            elif opcode == OPCODES['STORE']:
                reg = data2
                addr = data1
                reg_name = next((k for k, v in REGISTERS.items() if v == reg), f"R{reg}")
                instruction = f"STORE {reg_name} into [{addr}]"
            elif opcode == OPCODES['LOAD']:
                reg = data2
                addr = data1
                reg_name = next((k for k, v in REGISTERS.items() if v == reg), f"R{reg}")
                instruction = f"LOAD [{addr}] into {reg_name}"
            elif opcode == OPCODES['SET']:
                reg = data2
                value = data1
                reg_name = next((k for k, v in REGISTERS.items() if v == reg), f"R{reg}")
                instruction = f"SET {reg_name} to {value}"
            elif opcode == OPCODES['JUMP']:
                usereg = (data3 >> 4) & 0x1
                key = data3 & 0xF
                reg = (data2 >> 4) & 0xF
                jmptype = data2 & 0xF
                addr = data1
                
                # Get jump type name
                jmp_types = {v: k for k, v in JUMP_TYPES.items()}
                jmp_type_name = jmp_types.get(jmptype, "")
                
                if usereg:
                    reg_name = next((k for k, v in REGISTERS.items() if v == reg), f"R{reg}")
                    instruction = f"JUMP {reg_name}"
                else:
                    instruction = f"JUMP {addr}"
                
                if jmp_type_name:
                    instruction += f" if {jmp_type_name}"
                
                if jmptype == JUMP_TYPES['KEY']:
                    key_names = {v: k for k, v in KEYS.items()}
                    key_name = key_names.get(key, f"KEY{key}")
                    instruction += f" {key_name}"
            elif opcode == OPCODES['DRAW']:
                b = data3 & 0xF
                r = (data2 >> 4) & 0xF
                g = data2 & 0xF
                y = (data1 >> 4) & 0xF
                x = data1 & 0xF
                instruction = f"DRAW {x} {y} {r} {g} {b}"
            elif opcode == OPCODES['CLEARSCREEN']:
                instruction = "CLEARSCREEN"
            elif opcode == OPCODES['REFRESHSCREEN']:
                instruction = "REFRESHSCREEN"
            elif opcode == OPCODES['RANDOM']:
                reg = data2
                reg_name = next((k for k, v in REGISTERS.items() if v == reg), f"R{reg}")
                instruction = f"RANDOM {reg_name}"
            elif opcode == OPCODES['STACK']:
                stack_op = (data3 >> 2) & 0x3
                stack_type = data3 & 0x3
                reg = data2 & 0xF
                value = data1
                
                # Get stack operation and type names
                stack_ops = {v: k for k, v in STACK_OPS.items()}
                stack_types = {v: k for k, v in STACK_TYPES.items()}
                
                op_name = stack_ops.get(stack_op, f"OP{stack_op}")
                stack_name = stack_types.get(stack_type, f"STACK{stack_type}")
                
                if reg == 0xF:  # No register, use value
                    instruction = f"{op_name} {stack_name} {value}"
                else:
                    reg_name = next((k for k, v in REGISTERS.items() if v == reg), f"R{reg}")
                    instruction = f"{op_name} {stack_name} {reg_name}"
            else:
                instruction = f"UNKNOWN ({opcode})"
            
            # Write binary data and instruction
            f.write(f"{r2}{r1}  // {i:03d}: {instruction}\n")

    # Output to clipboard
    pyperclip.copy("\n".join(rom1))
    input("ROM1 (DATA1 + OPCODE) copied to clipboard. Press Enter to copy ROM2...")
    pyperclip.copy("\n".join(rom2))
    print("ROM2 (DATA3 + DATA2) copied to clipboard.")

if __name__ == '__main__':
    if len(sys.argv) > 1:
        assemble(sys.argv[1])
    else:
        assemble("C:/Users/kythe/Desktop/My-DLS/Assembler/code.asm")

# Instructions

**Note:** Most instructions that accept registers as destination or operands also accept special registers `ra` (return address) and `sp` (stack pointer), even if not specified in the following tables.

## Arithmetics Instructions

| Operator | (D)estination | (L)eft Operand | (R)ight Operand | Comment                         | Error          |
|:--------:|:-------------:|:--------------:|:---------------:|---------------------------------|----------------|
|  `add`   |      r?       |     r?/num     |     r?/num      | Adds L and R into D.            |                |
|  `sub`   |      r?       |     r?/num     |     r?/num      | Subtracts R from L into D.      |                |
|  `mul`   |      r?       |     r?/num     |     r?/num      | Multiplies L and R into D.      |                |
|  `div`   |      r?       |     r?/num     |     r?/num      | Stores L modulo R into D.       | `DivideByZero` |
|  `mod`   |      r?       |     r?/num     |     r?/num      | Stores square root of into D.   | `DivideByZero` |
|  `inc`   |      r?       |     r?/num     |                 | Increments L into D.            |                |
|  `dec`   |      r?       |     r?/num     |                 | Decrements L into D.            |                |
|  `pow`   |      r?       |     r?/num     |     r?/num      | Elevates L to power R into D.   |                |
|  `sqrt`  |      r?       |     r?/num     |                 | Stores square root of L into D. | `NaN`          |

## Bitwise Instructions

| Operator | (D)estination | (L)eft Operand | (R)ight Operand | Comment                                                         | Error |
|:--------:|:-------------:|:--------------:|:---------------:|-----------------------------------------------------------------|-------|
|  `not`   |      r?       |     r?/num     |                 | Performs a bitwise NOT of L and store result into D.            |       |
|  `and`   |      r?       |     r?/num     |     r?/num      | Performs a bitwise AND of L and R and store result into D.      |       |
|   `or`   |      r?       |     r?/num     |     r?/num      | Performs a bitwise OR of L and R and store result into D.       |       |
|  `xor`   |      r?       |     r?/num     |     r?/num      | Performs a bitwise XOR of L and R and store result into D.      |       |
|   `sl`   |      r?       |     r?/num     |     r?/num      | Shifts bits of L to R bits of the left and store result into D. |       |
|   `sr`   |      r?       |     r?/num     |     r?/num      | Shifts bits of L to R bits of the left and store result into D. |       |

## Devices Instructions

| Operator | (D)estination | (L)eft Operand | (R)ight Operand | Comment                                                     | Error                                               |
|:--------:|:-------------:|:--------------:|:---------------:|-------------------------------------------------------------|-----------------------------------------------------|
|   `ld`   |      r?       |      dD.R      |                 | Loads register R of device D and stores result in D.        | `DeviceOutOfBound` `DeviceUnplugged` `DeviceFailed` |
|   `st`   |     dD.R      |     r?/num     |                 | Set register R of device D and to the value stored in in D. | `DeviceOutOfBound` `DeviceUnplugged` `DeviceFailed` |

## Comparison Instructions

## Branching Instructions

## Debug Instructions

## Misc Instructions

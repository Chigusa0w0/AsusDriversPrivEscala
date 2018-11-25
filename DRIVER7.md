
# Basic Information
 
Author: Github @LimiQS
Contact: himeix@outlook.com
 
Name of product: NKeyUpdate
Version of product: 1.0.3.0
Description of product: ASUS Keyboard Adjustment Tool (for website download).[1]
 
Vulnerable part: driver7.sys (from NKeyUpdate 1.0.3.0)
Version of part: 2.5.0.1
Description of part: The driver for the ECtool driver-based tools
Part signed by: ASUSTeK Computer Inc. (‎Sign Date: ‎Thursday, ‎March ‎21, ‎2013)
 
Affected hardware: Asus ROG G752VS, etc.
 
# Summary
 
This post details a local privilege escalation (LPE) vulnerability I found in Asus’s NKeyUpdate version 1.0.3.0, which is still recommanded for some type of machine at present (e.g. G752VS). The bug is in a kernel driver loaded by the tool, and is pretty similar to bugs found by me in ATSZIO.sys[2], and those found by others in ASMMAP/ASMMAP64[3]. These bugs are pretty interesting because they can be used to bypass driver signature enforcement (DSE), make system loss of confidentiality, integrity or availability totally.
 
Asus’s NKeyUpdate is, according to the site, “ASUS Keyboard Adjustment Tool (…)”[1]. It’s primary purpose is to flash keyboard or BIOS firmware for Asus Laptops. There’s quite a lot of functionality in this driver itself. Includes:
- Map/Unmap physical memory
- Direct Port I/O
- Read/Write model-specific register (MSR)
- Read/Write PCI config
- Read/Write CPU registers
- etc.
 
# Bug
 
Calling this a “bug” is really a misnomer; the driver exposes this functionality eagerly. It actually exposes a lot of functionality, much like some of the previously mentioned drivers. It provides capabilities for reading and writing the model-specific register (MSR), map physical memory, and reading/writing PCI config.
 
The driver is first loaded when the NKeyUpdate tool is running, when some unknown condition met.
Once the driver is loaded, it exposes a symlink to the device at `` iteacc `` which is writable by unprivileged users on the system. This allows us to trigger one of the many IOCTLs exposed by the driver; approximately 30. After some reversing, I get a list of them, allowing me to extract the following: 
- // 0x9C40C008 = IOCTL_Win7Ready_OPERATION

- // 0x9C40E050 = IOCTL_Ext60Cmd
- // 0x9C40E054 = IOCTL_Ext62Cmd
- // 0x9C40E058 = IOCTL_Ext3rdCmd
- // 0x9C40E05C = IOCTL_EcB6Cmd
- // 0x9C40E060 = IOCTL_KpcR
- // 0x9C40E064 = IOCTL_KpcW

- // 0x9C40E080 = IOCTL_Kpc3R
- // 0x9C40E084 = IOCTL_Kpc3W
- // 0x9C40E088 = IOCTL_ReadRom
- // 0x9C40E08C = IOCTL_CH1RB_COMMAND
- // 0x9C40E090 = IOCTL_CH1WB_COMMAND
- // 0x9C40E094 = IOCTL_ReadRom_6064
- // 0x9C40E098 = IOCTL_Kpc4R
- // 0x9C40E09C = IOCTL_Kpc4W

- // 0x9C40E0C0 = IOCTL_Kpc3R_256
- // 0x9C40E0C4 = IOCTL_Kpc3W
- // 0x9C40E0C8 = IOCTL_Kpc4R_256
- // 0x9C40E0CC = IOCTL_Kpc4W

- // 0x9C40E400 = IOCTL_IOspace
- // 0x9C40E404 = IOCTL_IOindex
- // 0x9C40E408 = IOCTL_ReadPCI
- // 0x9C40E40C = IOCTL_ReadPCIE
- // 0x9C40E410 = IOCTL_EnumPCI
- // 0x9C40E414 = IOCTL_CPUCommand
- // 0x9C40E418 = IOCTL_ReadPCIn
- // 0x9C40E41C = IOCTL_ReadPCIn
- // 0x9C40E420 = IOCTL_MAPPHYSTOLIN <- Map physical memory to user mode!
- // 0x9C40E424 = IOCTL_UNMAPPHYSADDR <- Unmap

- // 0x9C40E440 = IOCTL_SoftSMI_SPT
- // 0x9C40E444 = IOCTL_NewAMemspace

There are already enough examples to prove that a vulnerability that can access physical memory freely is sufficient for confidentiality lost, privilege escalation[4] or to disable DSE[5], which could make an attacker to load unsigned driver for further exploit.

In addition, no "unlock" process is needed for calling IOCTLs mentioned above. This makes ``driver7.sys`` extremely undefended compared to other drivers with similar vulnerability. The only restriction is that it will only be functional on a computer manufactured by ASUS. It does this by verifying relevant information in SMBIOS.

However, even on a non-ASUS computer, IOCTL 0x9C40C008 will still work, but the effect will be to set all bits in the input buffer to ``1`` and write them to output buffer, which can be used to implement ``FillMemoryWith0xFF`` ;-)


# IoC

driver7.sys (x64) SHA256: 771A8D05F1AF6214E0EF0886662BE500EE910AB99F0154227067FDDCFE08A3DD
driver7.sys (x86 with DbgPrint) SHA256: 927C2A580D51A598177FA54C65E9D2610F5F212F1B6CB2FBF2740B64368F010A
driver7.sys (x86 without DbgPrint) SHA256: 42851A01469BA97CDC38939B10CF9EA13237AA1F6C37B1AC84904C5A12A81FA0

# Timeline

07/23/2018 - Vulnerability reported
08/14/2018 - Initial response from ASUS
10/21/2018 - 90 days has passed
11/05/2018 - 15 days grace period has passed. Public disclosure

# Resources

[1] https://www.asus.com/Laptops/ROG-G752VS/HelpDesk_Download/
[2] Not public disclosed at present.
[3] https://www.exploit-db.com/exploits/39785/
[4] https://github.com/hatRiot/bugs/blob/master/dell-support-assist/dell-sa-lpe.cpp 
[5] http://www.kernelmode.info/forum/viewtopic.php?f=11&t=3322
[6] https://www.exploit-db.com/exploits/8322/
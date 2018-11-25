# Basic Information

 

Author: Github @LimiQS

Contact: himeix#outlook.com (#→@)

 

Name of product: Asus WinFlash

Version of product: = 3.1.0

Description of product: Winflash is a necessary software to update the BIOS in Windows.[1]

 

Vulnerable part: [ATSZIO.sys](https://www.virustotal.com/#/file/9c2977d63faa340b03e1bbfb8a6db19c0adfa60ff6579b888ece10022c94c3ec/detection) & [ATSZIO64.sys](https://www.virustotal.com/#/file/01e024cb14b34b6d525c642a710bfa14497ea20fd287c39ba404b10a8b143ece/detection) (from WinFlash 3.1.0)

Version of part: 0.2.1.7

Description of part: ATSZIO Driver

Part signed by: ASUSTeK Computer Inc. (‎Sign Date: Thursday, ‎September ‎18, ‎2014)

 

# Summary

 

This post details a local privilege escalation (LPE) vulnerability I found in Asus’s WinFlash[0] tool version 3.1.0, which is still recommanded for some type of machine at present (e.g. G752VS, G800VI). The bug is in a kernel driver loaded by the tool, and is pretty similar to bugs found by Bryan Alexander in Dell SupportAssist Driver[2], and those found by others in ASMMAP/ASMMAP64[3]. These bugs are pretty interesting because they can be used to bypass driver signature enforcement (DSE), make system loss of confidentiality, integrity or availability totally.

 

Asus’s WinFlash is, according to the site, “(…) a necessary software to update the BIOS in Windows. (…)”[1]. It’s primary purpose is to flash BIOS firmware for Asus Laptops. There’s quite a lot of functionality in this driver itself. Includes:

- Map/Unmap physical memory

- Direct Port I/O

- Read Byte/Word/Dword/Block from desired physical memory address

- Allocate/Free contiguous memory

- Read/Write model-specific register (MSR)

- Read/Write PCI config

- Read/Write CPU registers

- etc.

 

# Bug

 

Calling this a “bug” is really a misnomer; the driver exposes this functionality eagerly. It actually exposes a lot of functionality, much like some of the previously mentioned drivers. It provides capabilities for reading and writing the model-specific register (MSR), map physical memory, and reading/writing PCI config.

 

The driver is first loaded when the WinFlash tool is launched, and the filename is ``ATSZIO64.sys`` on x64 and ``ATSZIO.sys`` on x86.

Once the driver is loaded, it exposes a symlink to the device at ATSZIO which is writable by unprivileged users on the system. This allows us to trigger one of the many IOCTLs exposed by the driver; approximately 20. After some reversing, I get a list of all of them, allowing me to extract the following:

- // 0x88070F58 = Read PCI CONFIG_SPACE

- // 0x88070F5C = Read PCI CONFIG_SPACE conditional

- // 0x88070F60 = Read desired port

- // 0x88070F64 = Write desired port

- // 0x88070F68 = IO desired port in byte half-duplex

- // 0x88070F6C = Write 2 Ports simplex

- // 0x88070F70 = Batch Read PCI config

- // 0x88070F74 = Batch read desired port simplex

- // 0x88070F78 = Batch IO desired port half-duplex

- // 0x88070F7C = Read desired physical address

- // 0x88070F80 = Write desired physical address

- // 0x88070F84 = Read Physical 4K block

- // 0x88070F88 = Read MSR

- // 0x88070F8C = Write MSR

- // 0x88070F90 = Alloc contiguous memory and get physical address

- // 0x88070F94 = Free contiguous memory

- // 0x88072000 = Get PCI config by Hal

- // 0x88072004 = Set PCI config by Hal

- // 0x8807200C = Map physical block into section

- // 0x88072010 = Unmap physical block

- // 0x88072014 = Read CPU registers

- // 0x88072018 = Write CPU registers

 

In addition, no "unlock" process is needed for calling IOCTLs mentioned above. This makes ``ATSZIO.sys`` extremely undefended compared to other drivers with similar vulnerability.

 

# Exploit

 

This PoC can dump a block of physical memory to disk, and write to a block of physical memory from a file.

I just modified slipstream's source[3] to build my own PoC and it's proved works.

There are already enough examples to prove that a vulnerability that can access physical memory is sufficient for confidentiality lost (since you could access kernel memory space), privilege escalation[4] or to disable DSE[5], which could make an attacker to load unsigned driver for further exploit.

 

To ASUS: If you never heard about "Dell SupportAssist Driver", you should keep an eye on NIST or CVE. If you've heard it before, how could you use these code without checking?

 

# Suggestion of fix

 

Remove vulnerable product from download site[6][7], or remove ATSZIO Driver from WinFlash.

 

# Timeline

 

07/23/2018 - Vulnerability reported
08/14/2018 - Initial response from ASUS
10/21/2018 - 90 days has passed
11/05/2018 - 15 days grace period has passed. Public disclosure

 

# References

 

[0] http://dlcdnet.asus.com/pub/ASUS/nb/Apps_for_Win10/Winflash/Winflash_Win10_64_VER310.zip

[1] https://www.asus.com/support/FAQ/1008276/

[2] http://hatriot.github.io/blog/2018/05/17/dell-supportassist-local-privilege-escalation/

[3] https://www.exploit-db.com/exploits/39785/

[4] https://github.com/hatRiot/bugs/blob/master/dell-support-assist/dell-sa-lpe.cpp

[5] http://www.kernelmode.info/forum/viewtopic.php?f=11&t=3322

[6] https://www.asus.com/Laptops/ROG-G752VS/HelpDesk_Download/

[7] https://www.asus.com/Laptops/ROG-G800VI/HelpDesk_Download/


#!/bin/bash
grep -q 'icm' Runtime/AssemblyInfo.cs || sed -i.bak 's/\(AssemblyVersion("\)\([^"]*\)\(")\)/\1\2-icm\3/' Runtime/AssemblyInfo.cs; grep -q 'icm' package.json || sed -i.bak 's/\("version": "\)\([^"]*\)\(".*\)/\1\2-icm\3/' package.json

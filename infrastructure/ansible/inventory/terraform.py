#!/usr/bin/env python3
"""Inventory sourced from Terraform outputs.

Reads the state through `terraform output` rather than the local state file,
so the same script works on the operator machine and on the CI runner that
never sees the state file.
"""
import json
import subprocess
import sys
from pathlib import Path

if "--host" in sys.argv:
    print("{}")
    sys.exit(0)

root = Path(__file__).resolve().parents[2] / "terraform-aws"

try:
    result = subprocess.run(
        ["terraform", f"-chdir={root}", "output", "-json", "servers"],
        capture_output=True,
        text=True,
        check=True,
    )
    servers = json.loads(result.stdout)
    public_ip = servers["public"]["public_ip"]
    public_private_ip = servers["public"]["private_ip"]
    private_ip = servers["private"]["private_ip"]
except (subprocess.CalledProcessError, FileNotFoundError, KeyError, TypeError) as exc:
    detail = getattr(exc, "stderr", "") or exc
    sys.exit(f"Chưa đọc được IP EC2 từ terraform output: {detail}")

if not public_ip or not private_ip or not public_private_ip:
    sys.exit("EC2 chưa có đủ IP trong Terraform state.")

print(json.dumps({
    "public": {"hosts": ["public_ec2"]},
    "private": {"hosts": ["private_ec2"]},
    "_meta": {
        "hostvars": {
            "public_ec2": {"ansible_host": public_ip, "private_ip": public_private_ip},
            "private_ec2": {"ansible_host": private_ip, "private_ip": private_ip},
        }
    },
}))
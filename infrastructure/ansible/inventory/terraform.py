#!/usr/bin/env python3
import json
import sys
from pathlib import Path

if "--host" in sys.argv:
    print("{}")
    sys.exit(0)

state_path = Path(__file__).resolve().parents[2] / "terraform-aws/terraform.tfstate"

try:
    state = json.loads(state_path.read_text(encoding="utf-8"))
    servers = state["outputs"]["servers"]["value"]
    public_ip = servers["public"]["public_ip"]
    private_ip = servers["private"]["private_ip"]
    public_private_ip = servers["public"]["private_ip"]
except (FileNotFoundError, KeyError, TypeError) as exc:
    sys.exit(f"Chưa đọc được IP EC2 từ {state_path}: {exc}")

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
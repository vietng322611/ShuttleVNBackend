#!/usr/bin/env python3
"""Inventory sourced from Terraform outputs.

Reads the state through `terraform output` rather than the local state file,
so the same script works on the operator machine and on the CI runner that
never sees the state file.
"""
import json
import os
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

# The private host is the CI runner, so converge runs from inside the VPC:
#  - the private host is the machine the play is running on, so connect
#    locally instead of SSHing to an address the private SG does not admit;
#  - the public host must be reached by private address, because its EIP
#    would send the packet out through the NAT gateway and back.
# The operator machine is outside the VPC and still needs the public address
# plus the ProxyJump from group_vars/private.yml.
inside_vpc = os.environ.get("ANSIBLE_INSIDE_VPC") == "1"

public_hostvars = {
    "ansible_host": public_private_ip if inside_vpc else public_ip,
    "public_ip": public_ip,
    "private_ip": public_private_ip,
}
private_hostvars = {"ansible_host": private_ip, "private_ip": private_ip}

if inside_vpc:
    public_hostvars["ansible_ssh_common_args"] = ""
    private_hostvars["ansible_connection"] = "local"

print(json.dumps({
    "public": {"hosts": ["public_ec2"]},
    "private": {"hosts": ["private_ec2"]},
    "_meta": {"hostvars": {"public_ec2": public_hostvars, "private_ec2": private_hostvars}},
}))
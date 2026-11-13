resource "aws_security_group" "public" {
  name        = "${var.project_name}-public-sg"
  description = "Frontend and reverse proxy"
  vpc_id      = var.vpc_id

  tags = {
    Name = "${var.project_name}-public-sg"
  }
}

resource "aws_security_group" "private" {
  name        = "${var.project_name}-private-sg"
  description = "Backend and monitoring"
  vpc_id      = var.vpc_id

  tags = {
    Name = "${var.project_name}-private-sg"
  }
}

resource "aws_vpc_security_group_ingress_rule" "public_ingress" {
  for_each = {
    ssh = {
      port = 22
      cidr = var.ssh_allowed_cidr
    }
  }

  security_group_id = aws_security_group.public.id
  description       = "Allow ${each.key}"
  ip_protocol       = "tcp"
  from_port         = each.value.port
  to_port           = each.value.port
  cidr_ipv4         = each.value.cidr
}

# Ports 80 and 443 are reachable from Cloudflare only
resource "aws_vpc_security_group_ingress_rule" "public_web" {
  for_each = {
    for pair in setproduct(["80", "443"], local.cloudflare_ipv4) :
    "${pair[0]}-${pair[1]}" => { port = tonumber(pair[0]), cidr = pair[1] }
  }

  security_group_id = aws_security_group.public.id
  description       = "Allow Cloudflare to reach port ${each.value.port}"
  ip_protocol       = "tcp"
  from_port         = each.value.port
  to_port           = each.value.port
  cidr_ipv4         = each.value.cidr
}

# Cloudflare's published ranges, see https://www.cloudflare.com/ips-v4
locals {
  cloudflare_ipv4 = [
    "173.245.48.0/20",
    "103.21.244.0/22",
    "103.22.200.0/22",
    "103.31.4.0/22",
    "141.101.64.0/18",
    "108.162.192.0/18",
    "190.93.240.0/20",
    "188.114.96.0/20",
    "197.234.240.0/22",
    "198.41.128.0/17",
    "162.158.0.0/15",
    "104.16.0.0/13",
    "104.24.0.0/14",
    "172.64.0.0/13",
    "131.0.72.0/22",
  ]
}

resource "aws_vpc_security_group_ingress_rule" "private_ingress" {
  for_each = {
    ssh     = 22
    backend = 8080
  }

  security_group_id            = aws_security_group.private.id
  referenced_security_group_id = aws_security_group.public.id
  description                  = "Allow ${each.key} from public SG"
  ip_protocol                  = "tcp"
  from_port                    = each.value
  to_port                      = each.value
}

resource "aws_vpc_security_group_ingress_rule" "public_metrics" {
  for_each = {
    node_exporter = 9100
    cadvisor      = 8081
  }

  security_group_id            = aws_security_group.public.id
  referenced_security_group_id = aws_security_group.private.id
  description                  = "Allow ${each.key} from private SG"
  ip_protocol                  = "tcp"
  from_port                    = each.value
  to_port                      = each.value
}

resource "aws_vpc_security_group_ingress_rule" "public_ssh_from_private" {
  security_group_id            = aws_security_group.public.id
  referenced_security_group_id = aws_security_group.private.id
  description                  = "Allow Ansible SSH from private SG"
  ip_protocol                  = "tcp"
  from_port                    = 22
  to_port                      = 22
}

resource "aws_vpc_security_group_egress_rule" "outbound" {
  for_each = {
    public  = aws_security_group.public.id
    private = aws_security_group.private.id
  }

  security_group_id = each.value
  description       = "Allow outbound traffic"
  ip_protocol       = "-1"
  cidr_ipv4         = "0.0.0.0/0"
}
data "aws_availability_zones" "available" {
  state = "available"

  filter {
    name   = "zone-type"
    values = ["availability-zone"]
  }
}

resource "aws_key_pair" "this" {
  key_name = "shuttlevn-ssh"

  public_key = trimspace(
    file(pathexpand(var.ssh_public_key_path))
  )

  tags = {
    Name = "shuttlevn-ssh"
  }
}

module "network" {
  source = "./modules/network"

  project_name       = var.project_name
  availability_zone  = data.aws_availability_zones.available.names[0]
  enable_nat_gateway = true
}

module "security" {
  source = "./modules/security"

  project_name     = var.project_name
  vpc_id           = module.network.vpc_id
  ssh_allowed_cidr = var.ssh_allowed_cidr
}

module "public_compute" {
  source = "./modules/compute"

  name               = "shuttlevn-public"
  ami_id             = var.ami_id
  instance_type      = "t3.small"
  subnet_id          = module.network.public_subnet_id
  security_group_ids = [module.security.public_sg_id]
  assign_public_ip   = true
  key_name           = aws_key_pair.this.key_name
}

module "private_compute" {
  source = "./modules/compute"

  name               = "shuttlevn-private"
  ami_id             = var.ami_id
  instance_type      = "t3.medium"
  subnet_id          = module.network.private_subnet_id
  security_group_ids = [module.security.private_sg_id]
  assign_public_ip   = false
  key_name           = aws_key_pair.this.key_name
}
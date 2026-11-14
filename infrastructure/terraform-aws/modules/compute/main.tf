resource "aws_instance" "this" {
  ami                         = var.ami_id
  instance_type               = var.instance_type
  subnet_id                   = var.subnet_id
  vpc_security_group_ids      = var.security_group_ids
  associate_public_ip_address = var.assign_public_ip
  key_name                    = var.key_name

  root_block_device {
    volume_type           = "gp3"
    volume_size           = 30
    encrypted             = true
    delete_on_termination = true
  }

  credit_specification {
    cpu_credits = "standard"
  }

  metadata_options {
    http_endpoint = "enabled"
    http_tokens   = "required"
  }

  tags = {
    Name = var.name
  }

  volume_tags = {
    Name = "${var.name}-root"
  }
}

# Cloudflare points at this address, so it has to survive a replace of the
# instance. Without it every rebuild silently breaks the edge with a 522.
resource "aws_eip" "this" {
  count    = var.assign_public_ip ? 1 : 0
  domain   = "vpc"
  instance = aws_instance.this.id

  tags = {
    Name = "${var.name}-eip"
  }
}
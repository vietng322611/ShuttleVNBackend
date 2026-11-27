resource "aws_eip" "nat" {
  count = var.enable_nat_gateway ? 1 : 0

  domain = "vpc"

  tags = {
    Name = "${var.project_name}-nat-eip"
  }
}

resource "aws_nat_gateway" "this" {
  count = var.enable_nat_gateway ? 1 : 0

  allocation_id     = aws_eip.nat[0].id
  subnet_id         = aws_subnet.public.id
  connectivity_type = "public"

  tags = {
    Name = "${var.project_name}-nat"
  }

  depends_on = [aws_internet_gateway.this]
}

resource "aws_route" "private_internet" {
  count = var.enable_nat_gateway ? 1 : 0

  route_table_id         = aws_route_table.private.id
  destination_cidr_block = "0.0.0.0/0"
  nat_gateway_id         = aws_nat_gateway.this[0].id
}

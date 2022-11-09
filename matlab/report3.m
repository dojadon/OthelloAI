range = 20;

c = zeros(1, range*2 + 1);

for k = 1:range
    c(range+k+1) = 1/(2*pi*k*1i) * (1 - exp(-k*pi*1i))^2;
    c(range-k+1) = -1/(2*pi*k*1i) * (1 - exp(k*pi*1i))^2;
end

x = -range:range;
bar(x, abs(c));
% bar(x, angle(c));
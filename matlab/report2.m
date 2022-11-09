range = 100

c = zeros(1, range*2 + 1);

for k = 1:range
    c(range+k) = 1/(2*pi*k*1i) * (1 - exp(-k*pi*1i))^2;
    c(range-k) = -1/(2*pi*k*1i) * (1 - exp(k*pi*1i))^2;
end

x = -range:range;
y = x * 0;

y_orignal = original(x);
% plot(x, y_orignal, 'DisplayName', 'original');

for k = 1:100
    y = y + c_p(k)*exp(1i*k*x) + c_m(k)*exp(1i*-k*x);

    if mod(k, 25) == 0
        subplot(2,2, k/25);

        plot(x, y_orignal, 'DisplayName', 'original');
        hold on
        plot(x, y, 'DisplayName', sprintf('k=%d', k));

        ylim([-2, 2])
        legend
    end
end

function out = original(x)
    out = x;

    mask = x <= pi;
    out(mask) = 1;

    mask = (pi < x) & (x <= 2*pi);
    out(mask) = -1;
end
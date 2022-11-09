a = zeros(1, 100);
b = zeros(1, 100);

for k = 1:100
    b(k) = 2/pi * (-g(k, 0) + 2*g(k, pi/2) - 2*g(k, 3*pi/2) + g(k, 2*pi)) - 6/k*cos(3*k*pi/2) + 2/k*cos(k*pi/2) + 4/k;
end

x = (1:1000)/1000 * pi*2;
y = x * 0;

y_orignal = original(x);
plot(x, y_orignal, 'DisplayName', 'original');

for k = 1:8
    y = y + a(k)*cos(k*x) + b(k)*sin(k*x);

    if mod(k, 2) == 0
        hold on
        plot(x, y, 'DisplayName', sprintf('k=%d', k));
    end
end

ylim([-3.5 3.5])
legend

function out = original(x)
    out = x;

    mask = x <= pi/2;
    out(mask) = 2 * x(mask);

    mask = (pi/2 < x) & (x <= 3*pi/2);
    out(mask) = -2 * (x(mask) - pi);

    mask = 3*pi/2 < x & x <= 2*pi;
    out(mask) = 2 * (x(mask) - 2*pi);
end

function out = g(k,t)
    out = -t/k * cos(k*t) + 1/(k*k) * sin(k*t);
end
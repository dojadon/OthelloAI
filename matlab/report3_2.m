
num_div = 500;

x = (-num_div:num_div)/num_div * 4;
y_sinc1 = sinc1(x);
y_sinc2 = sinc2(x);

subplot(1,2,1);
plot(x, y_sinc1);
[t,s] = title('f(t)');
t.FontSize = 16;

subplot(1,2,2);
plot(x, y_sinc2);
[t,s] = title('F(t)');
t.FontSize = 16;

function out = sinc1(x)
    out = 1./(pi*x) .* sin(pi*x);
end

function out = sinc2(x)
    out = 2./x .* sin(2*x);
end